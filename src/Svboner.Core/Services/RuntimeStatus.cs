using Svboner.Core.Buttplug;
using Svboner.Core.Mapping;
using Svboner.Core.Phd2;

namespace Svboner.Core.Services;

public sealed class RuntimeStatus
{
    public bool Phd2Connected      { get; init; }
    public string? Phd2Error       { get; init; }
    public Phd2AppState GuideState { get; init; }
    public bool IntifaceConnected  { get; init; }
    public string? IntifaceError   { get; init; }
    public IReadOnlyList<DeviceInfo> Devices    { get; init; } = [];
    public uint? SelectedDeviceIndex            { get; init; }
    public GuideMetrics? Metrics               { get; init; }
    public double OutputIntensity              { get; init; }
    public bool OutputEnabled                  { get; init; }
    public bool BurstActive                    { get; init; }
}
