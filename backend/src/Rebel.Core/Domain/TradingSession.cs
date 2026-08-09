namespace Rebel.Core.Domain;

public class TradingSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserId { get; init; } = default!;
    public Guid PlaybookId { get; init; }
    public string PlaybookName { get; set; } = default!;
    public string Instrument { get; set; } = default!;
    public AssetClass AssetClass { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Idle;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public decimal SessionPnl { get; set; }
    public int TradeCount { get; set; }
    public List<ConversationMessage> Messages { get; set; } = new();
    public List<Trading.TradingEvent> Events { get; set; } = new();
}
