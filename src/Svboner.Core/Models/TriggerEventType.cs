namespace Svboner.Core.Models;

/// <summary>Discrete PHD2 events that can fire a burst vibration action.</summary>
public enum TriggerEventType
{
    StarLost,
    AlertError,
    AlertWarning,
    SettleFailed,
    GuidingStopped,
    LockPositionLost,
    CalibrationFailed
}
