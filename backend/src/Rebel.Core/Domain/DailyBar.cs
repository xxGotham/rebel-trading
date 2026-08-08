namespace Rebel.Core.Domain;

/// <summary>One daily OHLCV bar. Date is the trading session date in US/Eastern.</summary>
public class DailyBar
{
    public long Id { get; set; }
    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }

    public DateOnly Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }

    /// <summary>Volume-weighted average price for the session, when the provider supplies it.</summary>
    public decimal? Vwap { get; set; }

    public decimal DollarVolume => Close * Volume;
}
