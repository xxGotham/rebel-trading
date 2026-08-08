using Rebel.Core.Signals;

namespace Rebel.Core.Scoring;

public sealed record FactorContribution(string Factor, double Raw, double ZScore, double Weight)
{
    public double Contribution => ZScore * Weight;
}

public sealed record ScoredCandidate
{
    public required SignalSet Signals { get; init; }
    public required bool Included { get; init; }
    public string? ExclusionReason { get; init; }

    /// <summary>Weighted sum of factor z-scores, normalised by total weight.</summary>
    public double CompositeZ { get; init; }

    /// <summary>Percentile-style 0–100 score derived from <see cref="CompositeZ"/>.</summary>
    public double Score { get; init; }

    public int Rank { get; init; }
    public string Strategy { get; init; } = string.Empty;
    public IReadOnlyList<FactorContribution> Factors { get; init; } = [];

    public string Symbol => Signals.Symbol;
}

/// <summary>
/// Cross-sectional factor scoring: filter, z-score each factor across the
/// surviving universe, combine with weights, rank.
/// <para>
/// Scoring is relative by design. "RSI 62" means nothing on its own; "top decile
/// relative volume among 100 liquid large caps this morning" is a signal. That
/// is also why every factor is z-scored against the same-day cohort rather than
/// against a historical threshold.
/// </para>
/// </summary>
public sealed class ScoringEngine
{
    private readonly ScoringWeights _weights;

    public ScoringEngine(ScoringWeights? weights = null) => _weights = weights ?? new ScoringWeights();

    public ScoringWeights Weights => _weights;

    public IReadOnlyList<ScoredCandidate> Score(IReadOnlyList<SignalSet> universe)
    {
        ArgumentNullException.ThrowIfNull(universe);

        var excluded = new List<ScoredCandidate>();
        var eligible = new List<SignalSet>();

        foreach (var s in universe)
        {
            var reason = FailedFilter(s);
            if (reason is null) eligible.Add(s);
            else excluded.Add(new ScoredCandidate { Signals = s, Included = false, ExclusionReason = reason });
        }

        if (eligible.Count == 0) return excluded;

        var factors = BuildFactorMatrix(eligible);
        var totalWeight = factors.Sum(f => Math.Abs(f.Weight));

        var scored = new List<ScoredCandidate>(eligible.Count);
        for (var i = 0; i < eligible.Count; i++)
        {
            var contributions = factors
                .Select(f => new FactorContribution(f.Name, f.Raw[i], f.Z[i], f.Weight))
                .ToArray();

            // Normalising by total weight keeps the composite on a z-like scale, so
            // MinimumScore stays meaningful when weights are retuned.
            var compositeZ = totalWeight <= 0
                ? 0d
                : contributions.Sum(c => c.Contribution) / totalWeight;

            scored.Add(new ScoredCandidate
            {
                Signals = eligible[i],
                Included = true,
                CompositeZ = compositeZ,
                Score = Math.Round(Statistics.NormalCdf(compositeZ) * 100d, 2),
                Factors = contributions,
                Strategy = StrategyClassifier.Classify(eligible[i], contributions)
            });
        }

        var ranked = scored
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Symbol, StringComparer.Ordinal)
            .Select((c, index) => c with { Rank = index + 1 })
            .ToList();

        ranked.AddRange(excluded);
        return ranked;
    }

    /// <summary>Included candidates clearing the score floor, best first.</summary>
    public IReadOnlyList<ScoredCandidate> SelectTop(IReadOnlyList<ScoredCandidate> scored, int count) =>
        scored
            .Where(c => c.Included && c.Score >= _weights.MinimumScore)
            .OrderBy(c => c.Rank)
            .Take(count)
            .ToArray();

    private string? FailedFilter(SignalSet s)
    {
        var f = _weights.Filters;

        if (s.BarCount < f.MinBars) return $"insufficient history ({s.BarCount} bars)";
        if (s.LastPrice < f.MinPrice) return $"price {s.LastPrice:F2} below minimum {f.MinPrice:F2}";
        if (s.AverageDollarVolume20 < f.MinAverageDollarVolume)
            return $"illiquid: {s.AverageDollarVolume20 / 1_000_000m:F1}M avg dollar volume";
        if (s.GapPercent > f.MaxGapPercent)
            return $"already gapped {s.GapPercent:F1}% — too extended to chase";
        if (f.RequireAboveSma50 && !s.AboveSma50) return "below 50-day average (long-only)";
        if (s.Rsi14 > f.MaxRsi) return $"RSI {s.Rsi14:F0} is parabolic";
        if (s.AtrPercent > f.MaxAtrPercent)
            return $"ATR {s.AtrPercent:F1}% of price — too volatile to size";

        return null;
    }

    private sealed record FactorColumn(string Name, double Weight, double[] Raw, double[] Z);

    private FactorColumn[] BuildFactorMatrix(IReadOnlyList<SignalSet> eligible)
    {
        FactorColumn Column(string name, double weight, Func<SignalSet, double> select)
        {
            var raw = eligible.Select(select).ToArray();
            var mean = Statistics.Mean(raw);
            var sd = Statistics.StandardDeviation(raw);
            var z = raw.Select(v => Statistics.ZScore(v, mean, sd)).ToArray();
            return new FactorColumn(name, weight, raw, z);
        }

        return
        [
            Column("momentum20", _weights.Momentum20, s => s.Momentum20),
            Column("relativeStrength", _weights.RelativeStrength, s => s.RelativeStrengthVsMarket),
            Column("sectorStrength", _weights.SectorStrength, s => s.SectorStrength),
            Column("relativeVolume", _weights.RelativeVolume, s => s.RelativeVolume),
            Column("gap", _weights.Gap, s => s.GapPercent),
            Column("trendQuality", _weights.TrendQuality, s => s.ExtensionAboveSma50),
            // Negated: less distance below the 20-day high is better.
            Column("proximityToHigh", _weights.ProximityToHigh, s => -s.PercentOff20DayHigh),
            Column("volatility", _weights.VolatilityPenalty, s => s.RealizedVolatility20),
            // Only overbought readings are penalised; below 70 this factor is flat.
            Column("overbought", _weights.OverboughtPenalty, s => Math.Max(0d, s.Rsi14 - 70d))
        ];
    }
}
