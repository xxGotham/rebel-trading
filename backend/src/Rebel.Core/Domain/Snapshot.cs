namespace Rebel.Core.Domain;

/// <summary>
/// Point-in-time quote taken at analysis time — typically pre-market.
/// Stored per run so a run can be replayed exactly as it saw the market.
/// </summary>
public class Snapshot
{
    public long Id { get; set; }
    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    public Guid? AnalysisRunId { get; set; }

    public DateTime AsOfUtc { get; set; }
    public decimal LastPrice { get; set; }
    public decimal PreviousClose { get; set; }

    /// <summary>Volume traded so far in the current session, including pre-market.</summary>
    public long SessionVolume { get; set; }

    public decimal? DayHigh { get; set; }
    public decimal? DayLow { get; set; }

    public decimal GapPercent =>
        PreviousClose == 0m ? 0m : (LastPrice - PreviousClose) / PreviousClose * 100m;
}
