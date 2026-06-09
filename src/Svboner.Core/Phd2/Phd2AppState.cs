namespace Svboner.Core.Phd2;

public enum Phd2AppState
{
    Unknown,
    Stopped,
    Selected,
    Calibrating,
    Guiding,
    LostLock,
    Paused,
    Looping
}
