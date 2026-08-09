namespace Rebel.Core.Trading;

public class TradingEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public Domain.TradingEventType EventType { get; set; }
    public string Description { get; set; } = default!;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
