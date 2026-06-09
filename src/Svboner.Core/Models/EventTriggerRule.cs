namespace Svboner.Core.Models;

/// <summary>
/// A rule that fires a timed vibration burst when a specific PHD2 event occurs.
/// Active burst overrides the continuous mapping output.
/// </summary>
public sealed class EventTriggerRule
{
    public bool Enabled { get; set; } = true;

    public TriggerEventType Event { get; set; } = TriggerEventType.StarLost;

    /// <summary>Burst intensity 0–1.</summary>
    public double Intensity { get; set; } = 1.0;

    /// <summary>How long the burst lasts in milliseconds.</summary>
    public int DurationMs { get; set; } = 3000;
}
