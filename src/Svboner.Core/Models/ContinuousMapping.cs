namespace Svboner.Core.Models;

/// <summary>
/// Maps a continuous PHD2 signal linearly to a vibration intensity range.
/// Inverse mapping (better guiding = more vibration) is achieved by setting
/// OutputLow greater than OutputHigh.
/// </summary>
public sealed class ContinuousMapping
{
    public bool Enabled { get; set; } = true;

    public SignalSource Source { get; set; } = SignalSource.RmsArcsec;

    /// <summary>Signal value that corresponds to OutputLow (e.g. 0 arcsec).</summary>
    public double InputLow { get; set; } = 0.0;

    /// <summary>Signal value that corresponds to OutputHigh (e.g. 2 arcsec RMS).</summary>
    public double InputHigh { get; set; } = 2.0;

    /// <summary>Intensity at InputLow, 0–1.</summary>
    public double OutputLow { get; set; } = 0.0;

    /// <summary>Intensity at InputHigh, 0–1.</summary>
    public double OutputHigh { get; set; } = 1.0;

    /// <summary>
    /// Exponential smoothing factor 0–1. Higher = smoother/slower response.
    /// 0 = no smoothing (instant).
    /// </summary>
    public double Smoothing { get; set; } = 0.3;
}
