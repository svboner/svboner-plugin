using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Svboner.Core.Phd2;

/// <summary>
/// Connects to PHD2's JSON-RPC event server over TCP, streams events as typed records,
/// and supports sending RPC method calls (used for get_pixel_scale).
/// </summary>
public sealed class Phd2Client : IAsyncDisposable
{
    private readonly ILogger<Phd2Client> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private int _nextId = 1;

    // Pending RPC responses keyed by request id.
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pending = new();

    public bool IsConnected => _tcp?.Connected == true;

    public Phd2AppState AppState { get; private set; } = Phd2AppState.Unknown;

    /// <summary>Pixel scale in arcsec/pixel, fetched on connect and on ConfigurationChange.</summary>
    public double PixelScaleArcsec { get; private set; } = 1.0;

    public event EventHandler<Phd2Event>? EventReceived;
    public event EventHandler<(bool Connected, string? Error)>? ConnectionChanged;

    public Phd2Client(ILogger<Phd2Client> logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        await DisposeConnectionAsync();

        _tcp = new TcpClient();
        await _tcp.ConnectAsync(host, port, ct);
        _stream = _tcp.GetStream();

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _readTask = ReadLoopAsync(_readCts.Token);

        try
        {
            PixelScaleArcsec = await GetPixelScaleAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch pixel scale from PHD2 — defaulting to 1.0 arcsec/px");
        }

        _logger.LogInformation("Connected to PHD2 {Host}:{Port} (pixel scale {Scale:F3} arcsec/px)",
            host, port, PixelScaleArcsec);
        ConnectionChanged?.Invoke(this, (true, null));
    }

    public async Task DisconnectAsync()
    {
        ConnectionChanged?.Invoke(this, (false, null));
        await DisposeConnectionAsync();
    }

    public async Task<double> GetPixelScaleAsync(CancellationToken ct = default)
    {
        var result = await CallAsync("get_pixel_scale", null, ct);
        return result.ValueKind == JsonValueKind.Number ? result.GetDouble() : PixelScaleArcsec;
    }

    // ── RPC ─────────────────────────────────────────────────────────────────

    private async Task<JsonElement> CallAsync(string method, object? parameters, CancellationToken ct)
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected to PHD2.");

        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_pending)
            _pending[id] = tcs;

        try
        {
            var req = parameters is null
                ? (object)new { method, id }
                : new { method, @params = parameters, id };

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req) + "\r\n");
            await _writeLock.WaitAsync(ct);
            try
            {
                await _stream.WriteAsync(bytes, ct);
            }
            finally
            {
                _writeLock.Release();
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            return await tcs.Task.WaitAsync(cts.Token);
        }
        catch
        {
            lock (_pending)
                _pending.Remove(id);
            throw;
        }
    }

    // ── Read loop ────────────────────────────────────────────────────────────

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await ReadLineAsync(ct);
                if (line is null) break;
                HandleLine(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PHD2 read loop ended unexpectedly");
            ConnectionChanged?.Invoke(this, (false, ex.Message));
        }
    }

    private void HandleLine(string line)
    {
        // Check if this is an RPC response (has "id" + "result"/"error", no "Event" key).
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Event", out _) && root.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetInt32();
                TaskCompletionSource<JsonElement>? tcs;
                lock (_pending)
                {
                    _pending.TryGetValue(id, out tcs);
                    _pending.Remove(id);
                }

                if (tcs is not null)
                {
                    if (root.TryGetProperty("result", out var result))
                        tcs.TrySetResult(result.Clone());
                    else if (root.TryGetProperty("error", out var err))
                        tcs.TrySetException(new InvalidOperationException(err.GetRawText()));
                    else
                        tcs.TrySetResult(default);
                }
                return;
            }
        }
        catch { /* fall through to event parsing */ }

        // Parse as an event notification.
        var parsed = Phd2EventParser.Parse(line);
        if (parsed is null) return;

        // Keep AppState up to date.
        if (parsed is Phd2AppStateEvent appStateEvt)
            AppState = appStateEvt.State;
        else
        {
            var derived = Phd2EventParser.AppStateFromEvent(parsed.EventName);
            if (derived.HasValue) AppState = derived.Value;
        }

        // Refresh pixel scale on config changes (e.g. user swaps equipment profile).
        if (parsed is Phd2GenericEvent { Name: "ConfigurationChange" })
            _ = RefreshPixelScaleAsync();

        EventReceived?.Invoke(this, parsed);
    }

    private async Task RefreshPixelScaleAsync()
    {
        try
        {
            PixelScaleArcsec = await GetPixelScaleAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to refresh pixel scale");
        }
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        if (_stream is null) return null;

        var buf = new List<byte>(256);
        var b = new byte[1];

        while (true)
        {
            var n = await _stream.ReadAsync(b.AsMemory(0, 1), ct);
            if (n == 0) return buf.Count > 0 ? Encoding.UTF8.GetString(buf.ToArray()) : null;

            if (b[0] == '\n') break;
            if (b[0] != '\r') buf.Add(b[0]);
        }

        return Encoding.UTF8.GetString(buf.ToArray());
    }

    private async Task DisposeConnectionAsync()
    {
        if (_readCts is not null)
        {
            await _readCts.CancelAsync();
            _readCts.Dispose();
            _readCts = null;
        }

        if (_readTask is not null)
        {
            try { await _readTask; } catch { }
            _readTask = null;
        }

        lock (_pending)
        {
            foreach (var tcs in _pending.Values)
                tcs.TrySetCanceled();
            _pending.Clear();
        }

        _stream?.Dispose();
        _stream = null;
        _tcp?.Dispose();
        _tcp = null;
        AppState = Phd2AppState.Unknown;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionAsync();
        _writeLock.Dispose();
    }
}
