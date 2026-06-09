namespace Svboner.Core.Phd2;

// Base record — all PHD2 events share EventName and Timestamp.
public abstract record Phd2Event(string EventName, double Timestamp);

public sealed record Phd2VersionEvent(
    double Timestamp,
    string PhdVersion,
    int MsgVersion
) : Phd2Event("Version", Timestamp);

public sealed record Phd2AppStateEvent(
    double Timestamp,
    Phd2AppState State
) : Phd2Event("AppState", Timestamp);

public sealed record Phd2GuideStepEvent(
    double Timestamp,
    int Frame,
    double Dx,
    double Dy,
    double RaDistanceRaw,
    double DecDistanceRaw,
    double Snr,
    double Hfd,
    double AvgDist
) : Phd2Event("GuideStep", Timestamp);

public sealed record Phd2StarLostEvent(
    double Timestamp,
    int Frame,
    double StarMass,
    double Snr,
    double AvgDist,
    int ErrorCode,
    string Status
) : Phd2Event("StarLost", Timestamp);

public sealed record Phd2AlertEvent(
    double Timestamp,
    string Message,
    string AlertType    // "info" | "question" | "warning" | "error"
) : Phd2Event("Alert", Timestamp);

public sealed record Phd2SettleDoneEvent(
    double Timestamp,
    int Status,         // 0 = success, non-zero = failed
    string? Error
) : Phd2Event("SettleDone", Timestamp);

// Catch-all for events we don't parse in detail (GuidingStopped, LockPositionLost, etc.)
public sealed record Phd2GenericEvent(
    double Timestamp,
    string Name
) : Phd2Event(Name, Timestamp);
