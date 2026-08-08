namespace Rebel.Core.Domain;

/// <summary>
/// A scored instrument from one run. Every instrument that survived the hard
/// filters gets a row — including the ones that were not recommended — so the
/// selection itself can be audited later.
/// </summary>
public class Candidate
{
    public long Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public AnalysisRun? AnalysisRun { get; set; }

    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public int Rank { get; set; }
    public double Score { get; set; }
    public bool Selected { get; set; }

    /// <summary>Why the instrument was dropped, if it was. Null when it passed.</summary>
    public string? ExclusionReason { get; set; }

    // Snapshot of the signals as of the run — denormalised on purpose: these are
    // the exact numbers the LLM was shown and the grader will be judged against.
    public decimal LastPrice { get; set; }
    public decimal PreviousClose { get; set; }
    public double GapPercent { get; set; }
    public double RelativeVolume { get; set; }
    public double Rsi14 { get; set; }
    public double Atr14 { get; set; }
    public double AtrPercent { get; set; }
    public double Sma20 { get; set; }
    public double Sma50 { get; set; }
    public double Momentum20 { get; set; }
    public double PercentOff20DayHigh { get; set; }
    public double RealizedVolatility20 { get; set; }
    public double RelativeStrengthVsMarket { get; set; }
    public double SectorStrength { get; set; }
    public long AverageVolume20 { get; set; }

    /// <summary>Per-factor z-scores and contributions as JSON, for explaining the rank.</summary>
    public string FactorBreakdownJson { get; set; } = "{}";

    public int NewsCount { get; set; }
}
