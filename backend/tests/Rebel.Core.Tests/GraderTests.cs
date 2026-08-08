using Rebel.Core.Domain;
using Rebel.Core.Performance;
using Xunit;

namespace Rebel.Core.Tests;

public class GraderTests
{
    private static readonly DateOnly Monday = new(2026, 3, 2);

    private static GradeInput Input(
        IReadOnlyList<DailyBar> bars,
        decimal entryLow = 99.5m,
        decimal entryHigh = 100.5m,
        decimal target = 105m,
        decimal stop = 97m,
        int holdingDays = 5,
        IReadOnlyList<DailyBar>? benchmark = null,
        int entryWindowDays = 2) => new()
        {
            TradingDate = Monday,
            EntryLow = entryLow,
            EntryHigh = entryHigh,
            Target = target,
            StopLoss = stop,
            HoldingPeriodDays = holdingDays,
            ForwardBars = bars,
            BenchmarkBars = benchmark ?? [],
            EntryWindowDays = entryWindowDays
        };

    [Fact]
    public void A_target_hit_is_graded_as_a_win_at_the_target_price()
    {
        var bars = TestData.Sessions(Monday,
            (100m, 101m, 99m, 100m),
            (101m, 106m, 100m, 105.5m));

        var result = Grader.Grade(Input(bars));

        Assert.NotNull(result);
        Assert.Equal(OutcomeStatus.TargetHit, result.Status);
        Assert.Equal(100m, result.EntryPrice);
        Assert.Equal(105m, result.ExitPrice);
        Assert.Equal(5m, result.ReturnPercent);
        // Risk was 3 points, reward 5 → 1.667R
        Assert.Equal(1.667m, result.RMultiple);
    }

    [Fact]
    public void A_stop_hit_is_graded_as_a_loss_at_the_stop_price()
    {
        var bars = TestData.Sessions(Monday,
            (100m, 101m, 99m, 100m),
            (99m, 99.5m, 96m, 96.5m));

        var result = Grader.Grade(Input(bars));

        Assert.NotNull(result);
        Assert.Equal(OutcomeStatus.StopHit, result.Status);
        Assert.Equal(97m, result.ExitPrice);
        Assert.Equal(-3m, result.ReturnPercent);
        Assert.Equal(-1m, result.RMultiple);
    }

    [Fact]
    public void A_bar_spanning_both_target_and_stop_is_graded_as_a_loss()
    {
        // Daily bars cannot tell us which level was touched first. Assuming the
        // favourable one would inflate the track record permanently, so the
        // pessimistic reading is the honest one.
        var bars = TestData.Sessions(Monday,
            (100m, 101m, 99m, 100m),
            (100m, 106m, 96m, 101m));

        var result = Grader.Grade(Input(bars));

        Assert.NotNull(result);
        Assert.Equal(OutcomeStatus.StopHit, result.Status);
    }

    [Fact]
    public void An_unresolved_trade_expires_at_the_close_of_the_holding_window()
    {
        var bars = TestData.Sessions(Monday,
            (100m, 101m, 99m, 100m),
            (100m, 102m, 99m, 101m),
            (101m, 103m, 100m, 102.5m));

        var result = Grader.Grade(Input(bars, holdingDays: 2));

        Assert.NotNull(result);
        Assert.Equal(OutcomeStatus.Expired, result.Status);
        Assert.Equal(102.5m, result.ExitPrice);
        Assert.Equal(2.5m, result.ReturnPercent);
    }

    [Fact]
    public void An_idea_that_never_reaches_the_entry_band_is_not_counted_as_a_loss()
    {
        var bars = TestData.Sessions(Monday,
            (90m, 91m, 89m, 90m),
            (90m, 92m, 89m, 91m));

        var result = Grader.Grade(Input(bars, entryLow: 99.5m, entryHigh: 100.5m));

        Assert.NotNull(result);
        Assert.Equal(OutcomeStatus.NeverTriggered, result.Status);
        Assert.Null(result.ReturnPercent);
    }

    [Fact]
    public void Grading_returns_null_while_the_outcome_is_still_undecided()
    {
        // Only one session has elapsed of a five-day window, and nothing resolved.
        var bars = TestData.Sessions(Monday, (100m, 101m, 99m, 100m));

        Assert.Null(Grader.Grade(Input(bars)));
    }

    [Fact]
    public void Grading_waits_before_declaring_an_idea_never_triggered()
    {
        var bars = TestData.Sessions(Monday, (90m, 91m, 89m, 90m));

        Assert.Null(Grader.Grade(Input(bars, entryWindowDays: 2)));
    }

    [Fact]
    public void Entry_fills_at_the_band_edge_the_price_came_from()
    {
        // Opens above the band and trades down into it: the fill is the top edge.
        var bars = TestData.Sessions(Monday,
            (103m, 104m, 99m, 100m),
            (100m, 106m, 99m, 105.5m));

        var result = Grader.Grade(Input(bars));

        Assert.NotNull(result);
        Assert.Equal(100.5m, result.EntryPrice);
    }

    [Fact]
    public void Excursions_record_how_far_the_trade_went_in_each_direction()
    {
        var bars = TestData.Sessions(Monday,
            (100m, 103m, 98m, 102m),
            (102m, 106m, 101m, 105.5m));

        var result = Grader.Grade(Input(bars));

        Assert.NotNull(result);
        Assert.Equal(6m, result.MaxFavorablePercent);   // high of 106 from an entry of 100
        Assert.Equal(-2m, result.MaxAdversePercent);    // low of 98
    }

    [Fact]
    public void Benchmark_return_covers_the_identical_window()
    {
        var bars = TestData.Sessions(Monday,
            (100m, 101m, 99m, 100m),
            (101m, 106m, 100m, 105.5m));

        var benchmark = TestData.Sessions(Monday,
            (500m, 505m, 498m, 500m),
            (500m, 512m, 500m, 510m));

        var result = Grader.Grade(Input(bars, benchmark: benchmark));

        Assert.NotNull(result);
        Assert.Equal(2m, result.BenchmarkReturnPercent);
        Assert.Equal(5m, result.ReturnPercent);
    }

    [Fact]
    public void Days_held_counts_sessions_between_entry_and_exit()
    {
        var bars = TestData.Sessions(Monday,
            (100m, 101m, 99m, 100m),
            (100m, 102m, 99m, 101m),
            (101m, 106m, 100m, 105.5m));

        var result = Grader.Grade(Input(bars));

        Assert.NotNull(result);
        Assert.Equal(2, result.DaysHeld);
    }

    [Fact]
    public void Grading_is_stamped_with_a_version()
    {
        // A change to the grading rules must be visible, not silent — otherwise a
        // rule tweak could quietly improve the historical track record.
        var bars = TestData.Sessions(Monday,
            (100m, 101m, 99m, 100m),
            (101m, 106m, 100m, 105.5m));

        Assert.Equal(Grader.Version, Grader.Grade(Input(bars))!.GraderVersion);
    }
}
