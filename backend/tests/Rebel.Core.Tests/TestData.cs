using Rebel.Core.Domain;
using Rebel.Core.Signals;

namespace Rebel.Core.Tests;

/// <summary>Builders that keep the tests readable and their intent obvious.</summary>
public static class TestData
{
    public static SignalSet Signals(
        string symbol = "TEST",
        decimal lastPrice = 100m,
        double atr = 2d,
        double momentum = 10d,
        double relativeVolume = 0.1d,
        double gapPercent = 1d,
        double rsi = 60d,
        double sma20 = 98d,
        double sma50 = 95d,
        double percentOffHigh = 2d,
        double realizedVolatility = 25d,
        double relativeStrength = 5d,
        double sectorStrength = 3d,
        decimal averageDollarVolume = 500_000_000m,
        int barCount = 200,
        decimal? high20 = null,
        decimal? low10 = null) => new()
        {
            InstrumentId = 1,
            Symbol = symbol,
            Sector = "Technology",
            TradingDate = new DateOnly(2026, 3, 2),
            AsOfUtc = new DateTime(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc),
            LastCompletedSession = new DateOnly(2026, 2, 27),
            BarCount = barCount,
            LastPrice = lastPrice,
            PreviousClose = lastPrice,
            GapPercent = gapPercent,
            RelativeVolume = relativeVolume,
            Rsi14 = rsi,
            Atr14 = atr,
            AtrPercent = lastPrice == 0m ? 0 : atr / (double)lastPrice * 100d,
            Sma20 = sma20,
            Sma50 = sma50,
            Momentum20 = momentum,
            PercentOff20DayHigh = percentOffHigh,
            High20Day = high20 ?? lastPrice * 1.02m,
            Low10Day = low10 ?? lastPrice * 0.97m,
            RealizedVolatility20 = realizedVolatility,
            RelativeStrengthVsMarket = relativeStrength,
            SectorStrength = sectorStrength,
            AverageVolume20 = 5_000_000,
            AverageDollarVolume20 = averageDollarVolume,
            ExtensionAboveSma50 = sma50 == 0 ? 0 : ((double)lastPrice - sma50) / sma50 * 100d
        };

    public static DailyBar Bar(
        DateOnly date, decimal open, decimal high, decimal low, decimal close, long volume = 1_000_000) =>
        new() { Date = date, Open = open, High = high, Low = low, Close = close, Volume = volume };

    /// <summary>Consecutive sessions starting at <paramref name="start"/>, skipping weekends and holidays.</summary>
    public static IReadOnlyList<DailyBar> Sessions(DateOnly start, params (decimal O, decimal H, decimal L, decimal C)[] bars)
    {
        var result = new List<DailyBar>();
        var date = start;

        foreach (var (o, h, l, c) in bars)
        {
            while (!Market.MarketCalendar.IsTradingDay(date)) date = date.AddDays(1);
            result.Add(Bar(date, o, h, l, c));
            date = date.AddDays(1);
        }

        return result;
    }
}
