using Buttplug.Client;
using Buttplug.Client.Connectors;
using Microsoft.Extensions.Logging;

namespace Svboner.Core.Buttplug;

/// <summary>
/// Wraps the Buttplug C# client. Connects to Intiface Central over WebSocket,
/// enumerates vibrating devices, and exposes a simple SetIntensityAsync / StopAllAsync API.
/// </summary>
public sealed class DeviceController : IAsyncDisposable
{
    private readonly ILogger<DeviceController> _logger;
    private ButtplugClient? _client;
    private readonly SemaphoreSlim _cmdLock = new(1, 1);
    private double _lastSent = -1;

    public bool IsConnected => _client?.Connected == true;

    public event EventHandler? DevicesChanged;
    public event EventHandler<(bool Connected, string? Error)>? ConnectionChanged;

    public DeviceController(ILogger<DeviceController> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DeviceInfo> GetDevices()
    {
        if (_client is null) return [];
        return _client.Devices
            .Select(d => new DeviceInfo(d.Index, d.Name, d.VibrateAttributes.Any()))
            .ToList();
    }

    public async Task ConnectAsync(string url, CancellationToken ct = default)
    {
        await DisposeClientAsync();

        _client = new ButtplugClient("SVBONER");
        _client.DeviceAdded   += (_, _) => DevicesChanged?.Invoke(this, EventArgs.Empty);
        _client.DeviceRemoved += (_, _) => DevicesChanged?.Invoke(this, EventArgs.Empty);
        _client.ErrorReceived += (_, e) =>
            _logger.LogWarning("Buttplug error: {Err}", e.Exception.Message);

        var connector = new ButtplugWebsocketConnector(new Uri(url));
        await _client.ConnectAsync(connector, ct);
        await _client.StartScanningAsync(ct);

        _logger.LogInformation("Connected to Intiface Central at {Url}", url);
        ConnectionChanged?.Invoke(this, (true, null));
    }

    public async Task DisconnectAsync()
    {
        ConnectionChanged?.Invoke(this, (false, null));
        await DisposeClientAsync();
    }

    /// <summary>
    /// Sends a vibration intensity command to the selected device (or first available vibrator).
    /// Skips the call if the intensity hasn't changed by more than 0.5%.
    /// </summary>
    public async Task SetIntensityAsync(uint? deviceIndex, double intensity, CancellationToken ct = default)
    {
        intensity = Math.Clamp(intensity, 0, 1);
        if (_client is null || !_client.Connected) return;

        var device = Resolve(deviceIndex);
        if (device is null) return;

        // Debounce tiny changes to avoid spamming the device.
        if (Math.Abs(_lastSent - intensity) < 0.005) return;

        await _cmdLock.WaitAsync(ct);
        try
        {
            await device.VibrateAsync(intensity);
            _lastSent = intensity;
        }
        finally
        {
            _cmdLock.Release();
        }
    }

    /// <summary>Immediately stops all vibrating devices. Safe to call at any time.</summary>
    public async Task StopAllAsync(CancellationToken ct = default)
    {
        if (_client is null) return;

        await _cmdLock.WaitAsync(ct);
        try
        {
            foreach (var d in _client.Devices.Where(d => d.VibrateAttributes.Any()))
            {
                try { await d.VibrateAsync(0.0); }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to stop device {Name}", d.Name);
                }
            }
            _lastSent = 0;
        }
        finally
        {
            _cmdLock.Release();
        }
    }

    private ButtplugClientDevice? Resolve(uint? index)
    {
        if (_client is null) return null;
        return index.HasValue
            ? _client.Devices.FirstOrDefault(d => d.Index == index.Value)
            : _client.Devices.FirstOrDefault(d => d.VibrateAttributes.Any());
    }

    private async Task DisposeClientAsync()
    {
        if (_client is null) return;
        try
        {
            await StopAllAsync();
            await _client.DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error during Buttplug disconnect");
        }
        _client.Dispose();
        _client = null;
        _lastSent = -1;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeClientAsync();
        _cmdLock.Dispose();
    }
}
