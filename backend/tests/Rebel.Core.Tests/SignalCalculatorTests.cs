using Rebel.Core.Domain;
using Rebel.Core.Signals;
using Xunit;

namespace Rebel.Core.Tests;

public class SignalCalculatorTests
{
    private static readonly DateOnly TradingDate = new(2026, 3, 2);

    private static SignalInput Input(IReadOnlyList<DailyBar> bars, Snapshot? snapshot = null) => new()
    {
        Instrument = new Instrument { Id = 1, Symbol = "TEST", Sector = "Technology", SectorEtf = "XLK" },
        Bars = bars,
        Snapshot = snapshot
    };

    /// <summary>Flat 100.00 history ending the session before the trading date.</summary>
    private static List<DailyBar> FlatHistory(int sessions = 120, decimal price = 100m)
    {
        var bars = new List<DailyBar>();
        var date = TradingDate;

        for (var i = 0; i < sessions; i++)
        {
            do { date = date.AddDays(-1); }
            while (!Market.MarketCalendar.IsTradingDay(date));

            bars.Add(TestData.Bar(date, price, price + 1m, price - 1m, price));
        }

        bars.Reverse();
        return bars;
    }

    [Fact]
    public void Bars_on_or_after_the_trading_date_are_never_used()
    {
        // The single most damaging bug this system could have: letting a
        // pre-market run see the session it is about to trade. A 500-point bar on
        // the trading date must leave every indicator untouched.
        var history = FlatHistory();
        var withFuture = history.Concat([
            TestData.Bar(TradingDate, 600m, 620m, 590m, 610m),
            TestData.Bar(TradingDate.AddDays(1), 700m, 720m, 690m, 710m)
        ]).ToList();

        var clean = SignalCalculator.Calculate(Input(history), TradingDate, DateTime.UtcNow).Signals!;
        var contaminated = SignalCalculator.Calculate(Input(withFuture), TradingDate, DateTime.UtcNow).Signals!;

        Assert.Equal(clean.Sma20, contaminated.Sma20, 8);
        Assert.Equal(clean.Sma50, contaminated.Sma50, 8);
        Assert.Equal(clean.Rsi14, contaminated.Rsi14, 8);
        Assert.Equal(clean.Atr14, contaminated.Atr14, 8);
        Assert.Equal(clean.Momentum20, contaminated.Momentum20, 8);
        Assert.Equal(clean.BarCount, contaminated.BarCount);
        Assert.Equal(clean.LastPrice, contaminated.LastPrice);
    }

    [Fact]
    public void The_last_completed_session_is_always_before_the_trading_date()
    {
        var signals = SignalCalculator.Calculate(Input(FlatHistory()), TradingDate, DateTime.UtcNow).Signals!;

        Assert.True(signals.LastCompletedSession < TradingDate);
    }

    [Fact]
    public void Too_little_history_is_reported_rather_than_scored_on()
    {
        var result = SignalCalculator.Calculate(Input(FlatHistory(sessions: 10)), TradingDate, DateTime.UtcNow);

        Assert.Null(result.Signals);
        Assert.False(result.Success);
        Assert.Contains("insufficient history", result.Failure);
    }

    [Fact]
    public void Without_a_snapshot_the_gap_is_zero_rather_than_fabricated()
    {
        var signals = SignalCalculator.Calculate(Input(FlatHistory()), TradingDate, DateTime.UtcNow).Signals!;

        Assert.Equal(0d, signals.GapPercent, 8);
        Assert.Equal(0d, signals.RelativeVolume, 8);
        Assert.Equal(100m, signals.LastPrice);
    }

    [Fact]
    public void A_snapshot_supplies_the_premarket_price_and_participation()
    {
        var snapshot = new Snapshot
        {
            AsOfUtc = DateTime.UtcNow,
            LastPrice = 103m,
            PreviousClose = 100m,
            SessionVolume = 200_000
        };

        var signals = SignalCalculator.Calculate(Input(FlatHistory(), snapshot), TradingDate, DateTime.UtcNow).Signals!;

        Assert.Equal(103m, signals.LastPrice);
        Assert.Equal(3d, signals.GapPercent, 6);
        Assert.Equal(0.2d, signals.RelativeVolume, 6);  // 200k against a 1M average
    }

    [Fact]
    public void Swing_levels_are_derived_from_the_history_window()
    {
        var history = FlatHistory();
        history[^3] = TestData.Bar(history[^3].Date, 100m, 118m, 99m, 100m);
        history[^2] = TestData.Bar(history[^2].Date, 100m, 101m, 82m, 100m);

        var signals = SignalCalculator.Calculate(Input(history), TradingDate, DateTime.UtcNow).Signals!;

        Assert.Equal(118m, signals.High20Day);
        Assert.Equal(82m, signals.Low10Day);
    }

    [Fact]
    public void As_of_returns_history_in_ascending_order()
    {
        var shuffled = FlatHistory(sessions: 5).OrderByDescending(b => b.Date).ToArray();

        var ordered = SignalCalculator.AsOf(shuffled, TradingDate);

        Assert.Equal(ordered.OrderBy(b => b.Date).Select(b => b.Date), ordered.Select(b => b.Date));
    }
}
