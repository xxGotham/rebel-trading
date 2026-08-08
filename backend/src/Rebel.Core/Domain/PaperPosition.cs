namespace Rebel.Core.Domain;

/// <summary>
/// A recommendation the user accepted into the simulated portfolio.
/// Distinct from <see cref="RecommendationOutcome"/>: outcomes grade every rec
/// automatically to measure the signal; paper positions track only what the
/// user chose to take, at the size they chose.
/// </summary>
public class PaperPosition
{
    public Guid Id { get; set; }
    public Guid RecommendationId { get; set; }
    public Recommendation? Recommendation { get; set; }

    public int InstrumentId { get; set; }
    public string Symbol { get; set; } = string.Empty;

    public PaperPositionStatus Status { get; set; }
    public TradeDirection Direction { get; set; } = TradeDirection.Long;

    public DateTime OpenedAtUtc { get; set; }
    public decimal EntryPrice { get; set; }
    public int Shares { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Target { get; set; }

    public DateTime? ClosedAtUtc { get; set; }
    public decimal? ExitPrice { get; set; }
    public string? ExitReason { get; set; }

    public decimal CostBasis => EntryPrice * Shares;
    public decimal? RealizedPnl => ExitPrice is null ? null : (ExitPrice.Value - EntryPrice) * Shares;
}
