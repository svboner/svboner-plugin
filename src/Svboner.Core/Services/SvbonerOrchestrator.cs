using Microsoft.Extensions.Logging;
using Svboner.Core.Buttplug;
using Svboner.Core.Config;
using Svboner.Core.Mapping;
using Svboner.Core.Phd2;

namespace Svboner.Core.Services;

/// <summary>
/// Top-level coordinator. Manages connection lifecycle for PHD2 and Intiface,
/// routes events through the mapping engine, and drives the device controller.
/// </summary>
public sealed class SvbonerOrchestrator : IAsyncDisposable
{
    private readonly ConfigStore _cfg;
    private readonly Phd2Client _phd2;
    private readonly DeviceController _devices;
    private readonly MappingEngine _engine = new();
    private readonly ILogger<SvbonerOrchestrator> _logger;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private DateTime _lastDeviceWrite = DateTime.MinValue;
    private double _lastWrittenIntensity = -1;

    public event EventHandler<RuntimeStatus>? StatusChanged;

    public SvbonerOrchestrator(
        ConfigStore cfg,
        Phd2Client phd2,
        DeviceController devices,
        ILogger<SvbonerOrchestrator> logger)
    {
        _cfg     = cfg;
        _phd2    = phd2;
        _devices = devices;
        _logger  = logger;

        _phd2.EventReceived     += OnPhd2Event;
        _phd2.ConnectionChanged += (_, e) => NotifyStatus(e.Error);
        _devices.ConnectionChanged += (_, e) => NotifyStatus(e.Error);
        _devices.DevicesChanged    += (_, _) => NotifyStatus();
    }

    public RuntimeStatus GetStatus() => BuildStatus();

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_loop is not null) return;
        _cts  = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loop = RunLoopAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is not null) { await _cts.CancelAsync(); _cts.Dispose(); _cts = null; }
        if (_loop is not null) { try { await _loop; } catch { } _loop = null; }
        await _devices.StopAllAsync();
        await _phd2.DisconnectAsync();
        await _devices.DisconnectAsync();
        _engine.Reset();
    }

    /// <summary>Called after config is replaced externally — notifies connected UI clients.</summary>
    public void ReloadConfig() => NotifyStatus();

    /// <summary>Immediately silences the device and disables output. Safe to call at any time.</summary>
    public async Task PanicStopAsync()
    {
        _cfg.Update(c => c.Global.OutputEnabled = false);
        _engine.Reset();
        await _devices.StopAllAsync();
        _lastWrittenIntensity = 0;
        NotifyStatus();
    }

    // ── Main tick loop ────────────────────────────────────────────────────────

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var config = _cfg.Get();

            if (!_phd2.IsConnected)
            {
                try { await _phd2.ConnectAsync(config.Global.Phd2Host, config.Global.Phd2Port, ct); }
                catch { await Delay(3000, ct); continue; }
            }

            if (!_devices.IsConnected)
            {
                try { await _devices.ConnectAsync(config.Global.IntifaceUrl, ct); }
                catch { await Delay(3000, ct); continue; }
            }

            _engine.Tick(config, _phd2.AppState);
            await FlushToDeviceAsync(config, ct);
            NotifyStatus();

            await Delay(50, ct);
        }
    }

    // ── PHD2 event handler ───────────────────────────────────────────────────

    private void OnPhd2Event(object? _, Phd2Event evt)
    {
        var config = _cfg.Get();
        if (evt is Phd2GuideStepEvent step)
            _engine.OnGuideStep(step, _phd2.PixelScaleArcsec, config);
        else
            _engine.OnEvent(evt, _phd2.AppState, config);

        _ = FlushToDeviceAsync(config, CancellationToken.None);
        NotifyStatus();
    }

    // ── Device output ─────────────────────────────────────────────────────────

    private async Task FlushToDeviceAsync(SvbonerConfig config, CancellationToken ct)
    {
        var intensity  = _engine.Output;
        var throttleMs = Math.Max(0, config.Global.UpdateThrottleMs);
        var now        = DateTime.UtcNow;

        var tooSoon   = throttleMs > 0 && (now - _lastDeviceWrite).TotalMilliseconds < throttleMs;
        var unchanged = Math.Abs(intensity - _lastWrittenIntensity) < 0.005;
        if (tooSoon && unchanged) return;

        try
        {
            await _devices.SetIntensityAsync(config.Global.SelectedDeviceIndex, intensity, ct);
            _lastWrittenIntensity = intensity;
            _lastDeviceWrite = now;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Device write failed");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void NotifyStatus(string? error = null) =>
        StatusChanged?.Invoke(this, BuildStatus(error));

    private RuntimeStatus BuildStatus(string? error = null)
    {
        var config = _cfg.Get();
        return new RuntimeStatus
        {
            Phd2Connected       = _phd2.IsConnected,
            Phd2Error           = error,
            GuideState          = _phd2.AppState,
            IntifaceConnected   = _devices.IsConnected,
            Devices             = _devices.GetDevices(),
            SelectedDeviceIndex = config.Global.SelectedDeviceIndex,
            Metrics             = _engine.LatestMetrics,
            OutputIntensity     = _engine.Output,
            OutputEnabled       = config.Global.OutputEnabled,
            BurstActive         = _engine.IsBurstActive,
        };
    }

    private static async Task Delay(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); } catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _phd2.EventReceived -= OnPhd2Event;
        await _phd2.DisposeAsync();
        await _devices.DisposeAsync();
    }
}
