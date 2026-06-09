using Svboner.Core.Models;
using Svboner.Core.Phd2;

namespace Svboner.Core.Mapping;

/// <summary>
/// Pure state machine: receives PHD2 events, applies the configured mapping rules,
/// and produces a single 0–1 intensity value for the device controller.
/// </summary>
public sealed class MappingEngine
{
    private readonly Queue<double> _rmsWindow = new();
    private double _smoothed;
    private double _output;
    private double _burstIntensity;
    private DateTime? _burstEndsAt;
    private DateTime _lastTickAt = DateTime.MinValue;

    public double Output => _output;
    public bool IsBurstActive => _burstEndsAt.HasValue && DateTime.UtcNow < _burstEndsAt.Value;
    public GuideMetrics? LatestMetrics { get; private set; }

    public void Reset()
    {
        _rmsWindow.Clear();
        _smoothed = 0;
        _output = 0;
        _burstIntensity = 0;
        _burstEndsAt = null;
        LatestMetrics = null;
    }

    /// <summary>Called on every PHD2 GuideStep event.</summary>
    public void OnGuideStep(Phd2GuideStepEvent step, double pixelScale, SvbonerConfig cfg)
    {
        var ra    = Math.Abs(step.RaDistanceRaw)  * pixelScale;
        var dec   = Math.Abs(step.DecDistanceRaw) * pixelScale;
        var total = Math.Sqrt(step.Dx * step.Dx + step.Dy * step.Dy) * pixelScale;
        var avg   = step.AvgDist * pixelScale;

        _rmsWindow.Enqueue(total);
        while (_rmsWindow.Count > Math.Max(1, cfg.Global.RmsWindowFrames))
            _rmsWindow.Dequeue();

        var rms = _rmsWindow.Count > 0
            ? Math.Sqrt(_rmsWindow.Average(x => x * x))
            : total;

        LatestMetrics = new GuideMetrics
        {
            TotalErrorArcsec = total,
            RaErrorArcsec    = ra,
            DecErrorArcsec   = dec,
            RmsArcsec        = rms,
            Snr              = step.Snr,
            AvgDistArcsec    = avg,
            Hfd              = step.Hfd,
        };

        Recompute(cfg, Phd2AppState.Guiding);
    }

    /// <summary>Called for every non-GuideStep PHD2 event (checks for trigger matches).</summary>
    public void OnEvent(Phd2Event evt, Phd2AppState state, SvbonerConfig cfg)
    {
        var trigger = MatchTrigger(evt);
        if (trigger.HasValue) FireTrigger(trigger.Value, cfg);
        Recompute(cfg, state);
    }

    /// <summary>Called periodically from the orchestrator tick loop.</summary>
    public void Tick(SvbonerConfig cfg, Phd2AppState state)
    {
        // Expire burst if its time has passed.
        if (_burstEndsAt.HasValue && DateTime.UtcNow >= _burstEndsAt.Value)
        {
            _burstEndsAt = null;
            _burstIntensity = 0;
        }
        Recompute(cfg, state);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void Recompute(SvbonerConfig cfg, Phd2AppState state)
    {
        if (!cfg.Global.OutputEnabled)
        {
            _output = ApplyRamp(0, cfg);
            return;
        }

        if (cfg.Global.OnlyWhileGuiding && state != Phd2AppState.Guiding)
        {
            _output = ApplyRamp(0, cfg);
            return;
        }

        double raw;

        if (IsBurstActive)
        {
            raw = _burstIntensity;
        }
        else if (cfg.Continuous.Enabled && LatestMetrics is not null)
        {
            var signal = PickSignal(cfg.Continuous.Source, LatestMetrics);
            var linear = MapLinear(signal,
                cfg.Continuous.InputLow, cfg.Continuous.InputHigh,
                cfg.Continuous.OutputLow, cfg.Continuous.OutputHigh);

            var s = Math.Clamp(cfg.Continuous.Smoothing, 0, 0.99);
            _smoothed = s > 0 ? _smoothed * s + linear * (1 - s) : linear;
            raw = Math.Clamp(_smoothed, 0, 1);
        }
        else
        {
            raw = 0;
        }

        _output = ApplyRamp(Math.Clamp(raw * cfg.Global.MasterMaxIntensity, 0, 1), cfg);
    }

    private double ApplyRamp(double target, SvbonerConfig cfg)
    {
        var now = DateTime.UtcNow;
        if (_lastTickAt == DateTime.MinValue) { _lastTickAt = now; return target; }

        var elapsed = (now - _lastTickAt).TotalSeconds;
        _lastTickAt = now;

        var rate = cfg.Global.RampRatePerSecond;
        if (rate <= 0 || elapsed <= 0) return target;

        var maxDelta = rate * elapsed;
        var diff = target - _output;
        return Math.Abs(diff) <= maxDelta ? target : _output + Math.Sign(diff) * maxDelta;
    }

    private void FireTrigger(TriggerEventType type, SvbonerConfig cfg)
    {
        var rule = cfg.Triggers.FirstOrDefault(r => r.Enabled && r.Event == type);
        if (rule is null) return;
        _burstIntensity = Math.Clamp(rule.Intensity, 0, 1);
        _burstEndsAt = DateTime.UtcNow.AddMilliseconds(rule.DurationMs);
    }

    private static TriggerEventType? MatchTrigger(Phd2Event evt) => evt switch
    {
        Phd2StarLostEvent                                   => TriggerEventType.StarLost,
        Phd2AlertEvent { AlertType: "error" }               => TriggerEventType.AlertError,
        Phd2AlertEvent { AlertType: "warning" }             => TriggerEventType.AlertWarning,
        Phd2SettleDoneEvent { Status: not 0 }               => TriggerEventType.SettleFailed,
        Phd2GenericEvent { Name: "GuidingStopped" }         => TriggerEventType.GuidingStopped,
        Phd2GenericEvent { Name: "LockPositionLost" }       => TriggerEventType.LockPositionLost,
        Phd2GenericEvent { Name: "CalibrationFailed" }      => TriggerEventType.CalibrationFailed,
        _ => null
    };

    private static double PickSignal(SignalSource src, GuideMetrics m) => src switch
    {
        SignalSource.TotalErrorArcsec => m.TotalErrorArcsec,
        SignalSource.RaErrorArcsec    => m.RaErrorArcsec,
        SignalSource.DecErrorArcsec   => m.DecErrorArcsec,
        SignalSource.RmsArcsec        => m.RmsArcsec,
        SignalSource.Snr              => m.Snr,
        SignalSource.AvgDistArcsec    => m.AvgDistArcsec,
        SignalSource.Hfd              => m.Hfd,
        _                             => m.RmsArcsec
    };

    private static double MapLinear(double v, double inLo, double inHi, double outLo, double outHi)
    {
        if (Math.Abs(inHi - inLo) < 1e-9) return outHi;
        var t = Math.Clamp((v - inLo) / (inHi - inLo), 0, 1);
        return outLo + t * (outHi - outLo);
    }
}
