namespace Svboner.Core.Models;

public sealed class SvbonerConfig
{
    public GlobalSettings Global { get; set; } = new();

    public ContinuousMapping Continuous { get; set; } = new();

    public List<EventTriggerRule> Triggers { get; set; } =
    [
        new EventTriggerRule
        {
            Event = TriggerEventType.StarLost,
            Intensity = 1.0,
            DurationMs = 5000,
            Enabled = true
        },
        new EventTriggerRule
        {
            Event = TriggerEventType.AlertError,
            Intensity = 0.7,
            DurationMs = 2000,
            Enabled = true
        }
    ];
}
