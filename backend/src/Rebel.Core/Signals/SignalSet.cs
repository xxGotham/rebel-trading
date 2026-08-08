namespace Rebel.Core.Signals;

/// <summary>
/// The complete quantitative picture of one instrument at one moment.
/// This is what gets scored, what gets persisted on the candidate row, and what
/// the LLM is shown. Nothing downstream is allowed to compute a number that
/// isn't derived from here.
/// </summary>
public sealed record SignalSet
{
    public required int InstrumentId { get; init; }
    public required string Symbol { get; init; }
    public required string Sector { get; init; }
    public required DateOnly TradingDate { get; init; }
    public required DateTime AsOfUtc { get; init; }

    /// <summary>Pre-market/last price if a snapshot was available, otherwise the prior close.</summary>
    public required decimal LastPrice { get; init; }
    public required decimal PreviousClose { get; init; }

    /// <summary>Last completed session used for the history. Never the session being traded.</summary>
    public required DateOnly LastCompletedSession { get; init; }

    public required int BarCount { get; init; }

    public double GapPercent { get; init; }

    /// <summary>
    /// Session volume so far over the 20-day average full-session volume.
    /// Pre-market this is a small number by construction — it is only meaningful
    /// cross-sectionally, i.e. against the same ratio for every other name in the
    /// universe measured at the same instant. The scoring engine z-scores it, so
    /// that is exactly how it gets used.
    /// </summary>
    public double RelativeVolume { get; init; }

    public double Rsi14 { get; init; }
    public double Atr14 { get; init; }

    /// <summary>ATR as a percent of price — the volatility unit used for stops and sizing.</summary>
    public double AtrPercent { get; init; }

    public double Sma20 { get; init; }
    public double Sma50 { get; init; }

    /// <summary>20-session rate of change, percent.</summary>
    public double Momentum20 { get; init; }

    /// <summary>How far below the 20-day high, percent. 0 means sitting at the high.</summary>
    public double PercentOff20DayHigh { get; init; }

    /// <summary>20-day high — the breakout level a swing target is measured against.</summary>
    public decimal High20Day { get; init; }

    /// <summary>10-day swing low — where a trend-following stop actually belongs.</summary>
    public decimal Low10Day { get; init; }

    public double RealizedVolatility20 { get; init; }

    /// <summary>20-day momentum minus the market's 20-day momentum.</summary>
    public double RelativeStrengthVsMarket { get; init; }

    /// <summary>20-day momentum of the instrument's sector ETF.</summary>
    public double SectorStrength { get; init; }

    public long AverageVolume20 { get; init; }
    public decimal AverageDollarVolume20 { get; init; }

    /// <summary>Percent distance of price above its 50-day average — the trend-quality factor.</summary>
    public double ExtensionAboveSma50 { get; init; }

    public bool AboveSma20 => (double)LastPrice > Sma20;
    public bool AboveSma50 => (double)LastPrice > Sma50;
    public bool UptrendStructure => Sma20 > Sma50 && AboveSma50;
}
