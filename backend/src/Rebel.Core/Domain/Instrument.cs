namespace Rebel.Core.Domain;

/// <summary>A tradable symbol in the Rebel universe.</summary>
public class Instrument
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;

    /// <summary>ETF used as the sector proxy for relative-strength (e.g. XLK for tech).</summary>
    public string SectorEtf { get; set; } = string.Empty;

    public string Exchange { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>True for benchmark/sector ETFs: ingested for context, never recommended.</summary>
    public bool IsBenchmark { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<DailyBar> Bars { get; set; } = [];
}
