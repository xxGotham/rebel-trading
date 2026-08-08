using Rebel.Core.Domain;
using Rebel.Core.Performance;
using Xunit;

namespace Rebel.Core.Tests;

public class PerformanceStatsTests
{
    private static GradedTrade Trade(
        decimal returnPercent,
        OutcomeStatus status = OutcomeStatus.TargetHit,
        string strategy = "Rebel Momentum",
        ConfidenceLevel confidence = ConfidenceLevel.High,
        decimal? rMultiple = null,
        int daysHeld = 3,
        decimal? benchmark = null,
        int day = 2) => new()
        {
            Symbol = "TEST",
            Strategy = strategy,
            Confidence = confidence,
            Status = status,
            TradingDate = new DateOnly(2026, 3, day),
            ReturnPercent = returnPercent,
            RMultiple = rMultiple,
            DaysHeld = daysHeld,
            BenchmarkReturnPercent = benchmark
        };

    [Fact]
    public void Win_rate_and_averages_reflect_the_graded_trades()
    {
        var stats = PerformanceStats.From([
            Trade(6m),
            Trade(4m),
            Trade(-3m, OutcomeStatus.StopHit),
            Trade(-3m, OutcomeStatus.StopHit)
        ]);

        Assert.Equal(4, stats.Trades);
        Assert.Equal(2, stats.Wins);
        Assert.Equal(2, stats.Losses);
        Assert.Equal(50m, stats.WinRate);
        Assert.Equal(1m, stats.AverageReturnPercent);
        Assert.Equal(5m, stats.AverageWinPercent);
        Assert.Equal(-3m, stats.AverageLossPercent);
        Assert.Equal(6m, stats.BestPercent);
        Assert.Equal(-3m, stats.WorstPercent);
    }

    [Fact]
    public void Ideas_that_never_triggered_are_excluded_from_the_statistics()
    {
        // They say nothing about signal quality — counting them either way would
        // distort the win rate.
        var stats = PerformanceStats.From([
            Trade(5m),
            Trade(0m, OutcomeStatus.NeverTriggered),
            Trade(0m, OutcomeStatus.NeverTriggered)
        ]);

        Assert.Equal(1, stats.Trades);
        Assert.Equal(2, stats.NeverTriggered);
        Assert.Equal(100m, stats.WinRate);
    }

    [Fact]
    public void Profit_factor_is_gross_profit_over_gross_loss()
    {
        var stats = PerformanceStats.From([
            Trade(10m),
            Trade(-5m, OutcomeStatus.StopHit)
        ]);

        Assert.Equal(2m, stats.ProfitFactor);
    }

    [Fact]
    public void Profit_factor_does_not_divide_by_zero_when_nothing_has_lost_yet()
    {
        var stats = PerformanceStats.From([Trade(4m), Trade(6m)]);

        Assert.Equal(10m, stats.ProfitFactor);
        Assert.Equal(0, stats.Losses);
    }

    [Fact]
    public void Expectancy_is_the_average_r_multiple()
    {
        var stats = PerformanceStats.From([
            Trade(6m, rMultiple: 2m),
            Trade(-3m, OutcomeStatus.StopHit, rMultiple: -1m),
            Trade(-3m, OutcomeStatus.StopHit, rMultiple: -1m)
        ]);

        // (2 - 1 - 1) / 3 = 0 → a system with no edge, stated plainly.
        Assert.Equal(0m, stats.Expectancy);
    }

    [Fact]
    public void Alpha_compares_against_the_benchmark_over_the_same_windows()
    {
        var stats = PerformanceStats.From([
            Trade(5m, benchmark: 1m),
            Trade(3m, benchmark: 1m)
        ]);

        Assert.Equal(4m, stats.AverageReturnPercent);
        Assert.Equal(1m, stats.AverageBenchmarkPercent);
        Assert.Equal(3m, stats.AlphaPercent);
    }

    [Fact]
    public void Compounded_return_chains_the_trades_rather_than_summing_them()
    {
        var stats = PerformanceStats.From([
            Trade(10m, day: 2),
            Trade(10m, day: 3)
        ]);

        // 1.1 × 1.1 = 1.21 → 21%, not 20%.
        Assert.Equal(21m, stats.CompoundedReturnPercent);
    }

    [Fact]
    public void Breakdown_splits_the_statistics_by_any_dimension()
    {
        var trades = new[]
        {
            Trade(5m, strategy: "Rebel Momentum"),
            Trade(-2m, OutcomeStatus.StopHit, strategy: "Rebel Momentum"),
            Trade(8m, strategy: "Rebel Catalyst")
        };

        var byStrategy = PerformanceStats.Breakdown(trades, t => t.Strategy);

        Assert.Equal(2, byStrategy.Count);
        Assert.Equal("Rebel Momentum", byStrategy[0].Segment);
        Assert.Equal(2, byStrategy[0].Trades);
        Assert.Equal(50m, byStrategy[0].WinRate);
        Assert.Equal(100m, byStrategy.Single(s => s.Segment == "Rebel Catalyst").WinRate);
    }

    [Fact]
    public void An_empty_history_reports_zeroes_instead_of_throwing()
    {
        var stats = PerformanceStats.From([]);

        Assert.Equal(0, stats.Trades);
        Assert.Equal(0m, stats.WinRate);
        Assert.Equal("all", stats.Segment);
    }
}
