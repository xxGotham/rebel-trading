using Rebel.Core.Domain;
using Rebel.Core.Indicators;

namespace Rebel.Core.Signals;

public sealed record SignalInput
{
    public required Instrument Instrument { get; init; }

    /// <summary>Daily history. May contain bars after the as-of date; they are discarded.</summary>
    public required IReadOnlyList<DailyBar> Bars { get; init; }

    public Snapshot? Snapshot { get; init; }
    public IReadOnlyList<DailyBar> MarketBars { get; init; } = [];
    public IReadOnlyList<DailyBar> SectorBars { get; init; } = [];
}

public sealed record SignalResult(SignalSet? Signals, string? Failure)
{
    public bool Success => Signals is not null;
}

/// <summary>
/// Turns raw bars into a <see cref="SignalSet"/>.
/// <para>
/// The one rule that matters here: <c>tradingDate</c> is the session being
/// analysed, and history is truncated to <em>strictly before</em> it. A
/// pre-market run must never see the bar of the day it is about to trade —
/// that single mistake would make every backtest and every win-rate number in
/// the system a lie.
/// </para>
/// </summary>
public static class SignalCalculator
{
    public const int MinimumBars = 60;

    private const int RsiPeriod = 14;
    private const int AtrPeriod = 14;
    private const int MomentumPeriod = 20;
    private const int VolumePeriod = 20;

    public static SignalResult Calculate(SignalInput input, DateOnly tradingDate, DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(input);

        var history = AsOf(input.Bars, tradingDate);
        if (history.Count < MinimumBars)
            return new SignalResult(null, $"insufficient history ({history.Count}/{MinimumBars} bars)");

        var closes = history.Select(b => (double)b.Close).ToArray();
        var highs = history.Select(b => (double)b.High).ToArray();
        var lows = history.Select(b => (double)b.Low).ToArray();
        var last = history[^1];

        var sma20 = TechnicalIndicators.Sma(closes, 20).Latest() ?? 0;
        var sma50 = TechnicalIndicators.Sma(closes, 50).Latest() ?? 0;
        var rsi = TechnicalIndicators.Rsi(closes, RsiPeriod).Latest() ?? 50;
        var atr = TechnicalIndicators.Atr(highs, lows, closes, AtrPeriod).Latest() ?? 0;
        var momentum = TechnicalIndicators.RateOfChange(closes, MomentumPeriod).Latest() ?? 0;
        var high20 = TechnicalIndicators.HighestHigh(highs, 20).Latest() ?? (double)last.High;
        var low10 = TechnicalIndicators.LowestLow(lows, 10).Latest() ?? (double)last.Low;
        var realizedVol = TechnicalIndicators.RealizedVolatility(closes, 20).Latest() ?? 0;

        var recentVolumes = history.TakeLast(VolumePeriod).Select(b => b.Volume).ToArray();
        var avgVolume = (long)recentVolumes.Average();
        var avgDollarVolume = history.TakeLast(VolumePeriod).Average(b => b.Close * b.Volume);

        // Snapshot is optional: without one we fall back to the last close, which
        // makes gap and relative volume zero rather than fabricating a move.
        var lastPrice = input.Snapshot?.LastPrice ?? last.Close;
        var previousClose = last.Close;
        var gapPercent = previousClose == 0m
            ? 0d
            : (double)((lastPrice - previousClose) / previousClose * 100m);

        var relativeVolume = avgVolume > 0 && input.Snapshot is not null
            ? (double)input.Snapshot.SessionVolume / avgVolume
            : 0d;

        var marketMomentum = MomentumOf(input.MarketBars, tradingDate);
        var sectorMomentum = MomentumOf(input.SectorBars, tradingDate);

        var signals = new SignalSet
        {
            InstrumentId = input.Instrument.Id,
            Symbol = input.Instrument.Symbol,
            Sector = input.Instrument.Sector,
            TradingDate = tradingDate,
            AsOfUtc = asOfUtc,
            LastCompletedSession = last.Date,
            BarCount = history.Count,
            LastPrice = lastPrice,
            PreviousClose = previousClose,
            GapPercent = gapPercent,
            RelativeVolume = relativeVolume,
            Rsi14 = rsi,
            Atr14 = atr,
            AtrPercent = lastPrice == 0m ? 0d : atr / (double)lastPrice * 100d,
            Sma20 = sma20,
            Sma50 = sma50,
            Momentum20 = momentum,
            PercentOff20DayHigh = high20 == 0 ? 0 : (high20 - (double)lastPrice) / high20 * 100d,
            High20Day = (decimal)high20,
            Low10Day = (decimal)low10,
            RealizedVolatility20 = realizedVol,
            RelativeStrengthVsMarket = momentum - marketMomentum,
            SectorStrength = sectorMomentum,
            AverageVolume20 = avgVolume,
            AverageDollarVolume20 = avgDollarVolume,
            ExtensionAboveSma50 = sma50 == 0 ? 0 : ((double)lastPrice - sma50) / sma50 * 100d
        };

        return new SignalResult(signals, null);
    }

    /// <summary>Bars strictly before the session being analysed, in ascending date order.</summary>
    public static IReadOnlyList<DailyBar> AsOf(IReadOnlyList<DailyBar> bars, DateOnly tradingDate) =>
        bars.Where(b => b.Date < tradingDate).OrderBy(b => b.Date).ToArray();

    private static double MomentumOf(IReadOnlyList<DailyBar> bars, DateOnly tradingDate)
    {
        var history = AsOf(bars, tradingDate);
        if (history.Count <= MomentumPeriod) return 0d;

        var closes = history.Select(b => (double)b.Close).ToArray();
        return TechnicalIndicators.RateOfChange(closes, MomentumPeriod).Latest() ?? 0d;
    }
}
