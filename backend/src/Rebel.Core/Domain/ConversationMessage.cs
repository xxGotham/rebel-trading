namespace Rebel.Core.Domain;

public class ConversationMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public string Text { get; set; } = default!;
    public bool IsUserQuestion { get; set; }
    public TradingEventType? RelatedEventType { get; set; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
