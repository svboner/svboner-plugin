namespace Svboner.Core.Mapping;

/// <summary>A snapshot of the most recent computed guide metrics (all in arcseconds).</summary>
public sealed class GuideMetrics
{
    public double TotalErrorArcsec { get; init; }
    public double RaErrorArcsec   { get; init; }
    public double DecErrorArcsec  { get; init; }
    public double RmsArcsec       { get; init; }
    public double Snr             { get; init; }
    public double AvgDistArcsec   { get; init; }
    public double Hfd             { get; init; }
    public DateTime UpdatedAt     { get; init; } = DateTime.UtcNow;
}
