using Rebel.Core.Scoring;
using Xunit;

namespace Rebel.Core.Tests;

public class ScoringTests
{
    [Fact]
    public void Filters_exclude_with_a_stated_reason_rather_than_a_bad_score()
    {
        var engine = new ScoringEngine();

        var scored = engine.Score([
            TestData.Signals("PENNY", lastPrice: 2m),
            TestData.Signals("THIN", averageDollarVolume: 1_000_000m),
            TestData.Signals("GAPPED", gapPercent: 15d),
            TestData.Signals("PARABOLIC", rsi: 92d),
            TestData.Signals("WILD", lastPrice: 100m, atr: 30d),
            TestData.Signals("BELOWMA", lastPrice: 90m, sma50: 100d),
            TestData.Signals("GOOD")
        ]);

        string? ReasonFor(string symbol) => scored.Single(c => c.Symbol == symbol).ExclusionReason;

        Assert.Contains("below minimum", ReasonFor("PENNY"));
        Assert.Contains("illiquid", ReasonFor("THIN"));
        Assert.Contains("too extended", ReasonFor("GAPPED"));
        Assert.Contains("parabolic", ReasonFor("PARABOLIC"));
        Assert.Contains("too volatile", ReasonFor("WILD"));
        Assert.Contains("below 50-day", ReasonFor("BELOWMA"));
        Assert.Null(ReasonFor("GOOD"));
    }

    [Fact]
    public void Every_instrument_appears_in_the_output_including_the_excluded_ones()
    {
        // The audit trail depends on this: a name that was screened out must still
        // be explainable after the fact.
        var engine = new ScoringEngine();

        var scored = engine.Score([
            TestData.Signals("A"),
            TestData.Signals("B", lastPrice: 1m),
            TestData.Signals("C", momentum: 30d)
        ]);

        Assert.Equal(3, scored.Count);
        Assert.Equal(2, scored.Count(c => c.Included));
    }

    [Fact]
    public void Stronger_momentum_and_relative_strength_rank_higher()
    {
        var engine = new ScoringEngine();

        var scored = engine.Score([
            TestData.Signals("WEAK", momentum: 1d, relativeStrength: -3d, percentOffHigh: 12d),
            TestData.Signals("STRONG", momentum: 25d, relativeStrength: 15d, percentOffHigh: 0.5d),
            TestData.Signals("MIDDLE", momentum: 10d, relativeStrength: 5d, percentOffHigh: 5d)
        ]);

        var ranked = scored.Where(c => c.Included).OrderBy(c => c.Rank).Select(c => c.Symbol).ToArray();

        Assert.Equal(["STRONG", "MIDDLE", "WEAK"], ranked);
        Assert.Equal(1, scored.Single(c => c.Symbol == "STRONG").Rank);
    }

    [Fact]
    public void Identical_candidates_all_score_at_the_midpoint()
    {
        // With zero cross-sectional dispersion there is no information to rank on,
        // and the score should say so rather than inventing a winner.
        var engine = new ScoringEngine();

        var scored = engine.Score([
            TestData.Signals("A"),
            TestData.Signals("B"),
            TestData.Signals("C")
        ]);

        Assert.All(scored, c => Assert.Equal(50d, c.Score, 1));
    }

    [Fact]
    public void Scores_stay_within_0_to_100()
    {
        var engine = new ScoringEngine();
        var universe = Enumerable.Range(0, 40)
            .Select(i => TestData.Signals($"S{i}", momentum: i * 2d, relativeStrength: i - 20d))
            .ToArray();

        var scored = engine.Score(universe);

        Assert.All(scored.Where(c => c.Included), c =>
        {
            Assert.InRange(c.Score, 0d, 100d);
        });
    }

    [Fact]
    public void Select_top_honours_the_score_floor()
    {
        var weights = new ScoringWeights { MinimumScore = 99d };
        var engine = new ScoringEngine(weights);

        var scored = engine.Score([TestData.Signals("A"), TestData.Signals("B")]);

        Assert.Empty(engine.SelectTop(scored, 5));
    }

    [Fact]
    public void Select_top_returns_the_best_names_in_rank_order()
    {
        var engine = new ScoringEngine(new ScoringWeights { MinimumScore = 0d });

        var scored = engine.Score([
            TestData.Signals("LOW", momentum: 1d, relativeStrength: -5d),
            TestData.Signals("HIGH", momentum: 30d, relativeStrength: 20d),
            TestData.Signals("MID", momentum: 12d, relativeStrength: 4d)
        ]);

        var top = engine.SelectTop(scored, 2);

        Assert.Equal(2, top.Count);
        Assert.Equal("HIGH", top[0].Symbol);
        Assert.Equal("MID", top[1].Symbol);
    }

    [Fact]
    public void Factor_breakdown_explains_the_composite()
    {
        var engine = new ScoringEngine();
        var scored = engine.Score([
            TestData.Signals("A", momentum: 20d),
            TestData.Signals("B", momentum: 2d)
        ]);

        var winner = scored.First(c => c.Included && c.Rank == 1);

        Assert.Contains(winner.Factors, f => f.Factor == "momentum20");
        Assert.Contains(winner.Factors, f => f.Factor == "relativeVolume");
        Assert.NotEmpty(winner.Strategy);
    }

    [Fact]
    public void An_empty_universe_returns_no_candidates_instead_of_throwing()
    {
        Assert.Empty(new ScoringEngine().Score([]));
    }

    [Fact]
    public void Config_hash_changes_when_the_weights_change()
    {
        var baseline = new ScoringWeights();

        Assert.Equal(baseline.ComputeHash(), new ScoringWeights().ComputeHash());
        Assert.NotEqual(baseline.ComputeHash(), (baseline with { Momentum20 = 2.0 }).ComputeHash());
        Assert.NotEqual(
            baseline.ComputeHash(),
            (baseline with { Filters = baseline.Filters with { MaxRsi = 70 } }).ComputeHash());
    }

    [Fact]
    public void Z_scores_are_winsorised_so_one_outlier_cannot_dominate()
    {
        Assert.Equal(3d, Statistics.ZScore(value: 1000, mean: 0, stdDev: 1));
        Assert.Equal(-3d, Statistics.ZScore(value: -1000, mean: 0, stdDev: 1));
        Assert.Equal(0d, Statistics.ZScore(value: 5, mean: 5, stdDev: 0));
    }

    [Fact]
    public void Normal_cdf_maps_z_scores_onto_percentiles()
    {
        Assert.Equal(0.5d, Statistics.NormalCdf(0), 4);
        Assert.Equal(0.8413d, Statistics.NormalCdf(1), 3);
        Assert.Equal(0.1587d, Statistics.NormalCdf(-1), 3);
    }
}
