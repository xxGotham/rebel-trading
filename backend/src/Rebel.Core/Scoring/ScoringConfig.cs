using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Rebel.Core.Scoring;

/// <summary>
/// Hard filters applied before scoring. A name failing any of these is excluded
/// with a recorded reason rather than being scored badly — a filter is a
/// statement about tradability, not about attractiveness.
/// </summary>
public sealed record UniverseFilters
{
    public decimal MinPrice { get; init; } = 5m;
    public decimal MinAverageDollarVolume { get; init; } = 20_000_000m;
    public int MinBars { get; init; } = 60;

    /// <summary>Don't chase names that already gapped hard — the risk/reward is gone by the open.</summary>
    public double MaxGapPercent { get; init; } = 8d;

    /// <summary>Long-only V1: no counter-trend entries.</summary>
    public bool RequireAboveSma50 { get; init; } = true;

    /// <summary>Above this, the move is parabolic and a swing entry is late.</summary>
    public double MaxRsi { get; init; } = 80d;

    /// <summary>Above this, an ATR-based stop is too wide to size sensibly.</summary>
    public double MaxAtrPercent { get; init; } = 12d;
}

/// <summary>
/// Factor weights for the composite score. Weights are relative, not required to
/// sum to anything. Negative weights penalise.
/// </summary>
public sealed record ScoringWeights
{
    /// <summary>Bump this whenever weights or filters change meaning — analytics are grouped by it.</summary>
    public string Version { get; init; } = "score-v1";

    public double Momentum20 { get; init; } = 1.0;
    public double RelativeStrength { get; init; } = 1.5;
    public double SectorStrength { get; init; } = 0.5;
    public double RelativeVolume { get; init; } = 1.25;
    public double Gap { get; init; } = 0.75;
    public double TrendQuality { get; init; } = 0.75;
    public double ProximityToHigh { get; init; } = 1.0;
    public double VolatilityPenalty { get; init; } = -0.5;
    public double OverboughtPenalty { get; init; } = -0.5;

    public UniverseFilters Filters { get; init; } = new();

    /// <summary>Composite 0–100 floor a name must clear to be worth showing the LLM.</summary>
    public double MinimumScore { get; init; } = 55d;

    /// <summary>Stable hash of the effective configuration, stamped onto every run.</summary>
    public string ComputeHash()
    {
        var f = Filters;
        var payload = string.Join('|',
            Version,
            Fmt(Momentum20), Fmt(RelativeStrength), Fmt(SectorStrength), Fmt(RelativeVolume),
            Fmt(Gap), Fmt(TrendQuality), Fmt(ProximityToHigh), Fmt(VolatilityPenalty),
            Fmt(OverboughtPenalty), Fmt(MinimumScore),
            Fmt((double)f.MinPrice), Fmt((double)f.MinAverageDollarVolume), f.MinBars,
            Fmt(f.MaxGapPercent), f.RequireAboveSma50, Fmt(f.MaxRsi), Fmt(f.MaxAtrPercent));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static string Fmt(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}
