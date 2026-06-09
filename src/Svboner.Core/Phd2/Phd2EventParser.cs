using System.Text.Json;

namespace Svboner.Core.Phd2;

/// <summary>Parses raw PHD2 JSON lines into typed Phd2Event records.</summary>
public static class Phd2EventParser
{
    public static Phd2Event? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("Event", out var eventProp))
                return null;

            var name = eventProp.GetString() ?? string.Empty;
            var ts = root.TryGetProperty("Timestamp", out var t) ? t.GetDouble() : 0;

            return name switch
            {
                "Version" => new Phd2VersionEvent(
                    ts,
                    Str(root, "PHDVersion"),
                    Int(root, "MsgVersion")),

                "AppState" => new Phd2AppStateEvent(
                    ts,
                    ParseState(Str(root, "State"))),

                "GuideStep" => new Phd2GuideStepEvent(
                    ts,
                    Int(root, "Frame"),
                    Dbl(root, "dx"),
                    Dbl(root, "dy"),
                    Dbl(root, "RADistanceRaw"),
                    Dbl(root, "DECDistanceRaw"),
                    Dbl(root, "SNR"),
                    Dbl(root, "HFD"),
                    Dbl(root, "AvgDist")),

                "StarLost" => new Phd2StarLostEvent(
                    ts,
                    Int(root, "Frame"),
                    Dbl(root, "StarMass"),
                    Dbl(root, "SNR"),
                    Dbl(root, "AvgDist"),
                    Int(root, "ErrorCode"),
                    Str(root, "Status")),

                "Alert" => new Phd2AlertEvent(
                    ts,
                    Str(root, "Msg"),
                    Str(root, "Type", "info")),

                "SettleDone" => new Phd2SettleDoneEvent(
                    ts,
                    Int(root, "Status"),
                    root.TryGetProperty("Error", out var err) ? err.GetString() : null),

                _ => new Phd2GenericEvent(ts, name)
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Infer the new AppState from an event name, per the PHD2 spec table.</summary>
    public static Phd2AppState? AppStateFromEvent(string eventName) => eventName switch
    {
        "GuideStep"              => Phd2AppState.Guiding,
        "Paused"                 => Phd2AppState.Paused,
        "StartCalibration"       => Phd2AppState.Calibrating,
        "LoopingExposures"       => Phd2AppState.Looping,
        "LoopingExposuresStopped"=> Phd2AppState.Stopped,
        "StarLost"               => Phd2AppState.LostLock,
        _                        => null
    };

    public static Phd2AppState ParseState(string? s) => s switch
    {
        "Stopped"     => Phd2AppState.Stopped,
        "Selected"    => Phd2AppState.Selected,
        "Calibrating" => Phd2AppState.Calibrating,
        "Guiding"     => Phd2AppState.Guiding,
        "LostLock"    => Phd2AppState.LostLock,
        "Paused"      => Phd2AppState.Paused,
        "Looping"     => Phd2AppState.Looping,
        _             => Phd2AppState.Unknown
    };

    private static string Str(JsonElement e, string key, string def = "") =>
        e.TryGetProperty(key, out var p) ? p.GetString() ?? def : def;

    private static double Dbl(JsonElement e, string key) =>
        e.TryGetProperty(key, out var p) ? p.GetDouble() : 0;

    private static int Int(JsonElement e, string key) =>
        e.TryGetProperty(key, out var p) ? p.GetInt32() : 0;
}
