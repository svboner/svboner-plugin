namespace Svboner.Core.Models;

public sealed class GlobalSettings
{
    public string Phd2Host { get; set; } = "127.0.0.1";

    public int Phd2Port { get; set; } = 4400;

    public string IntifaceUrl { get; set; } = "ws://127.0.0.1:12345/buttplug";

    /// <summary>Selected Buttplug device index. Null = use the first vibrating device found.</summary>
    public uint? SelectedDeviceIndex { get; set; }

    /// <summary>Hard ceiling applied to all output, 0–1. Defaults conservatively to 0.8.</summary>
    public double MasterMaxIntensity { get; set; } = 0.8;

    /// <summary>When true, continuous output is silenced unless PHD2 is actively guiding.</summary>
    public bool OnlyWhileGuiding { get; set; } = true;

    /// <summary>Minimum milliseconds between device updates (rate limiting).</summary>
    public int UpdateThrottleMs { get; set; } = 100;

    /// <summary>Maximum intensity change per second. 0 = unlimited.</summary>
    public double RampRatePerSecond { get; set; } = 2.0;

    /// <summary>Number of guide frames used for the rolling RMS calculation.</summary>
    public int RmsWindowFrames { get; set; } = 30;

    /// <summary>Master on/off for the output engine. Defaults off — must be explicitly enabled each session.</summary>
    public bool OutputEnabled { get; set; } = false;

    /// <summary>Port for the local web UI.</summary>
    public int WebPort { get; set; } = 8787;
}
