namespace Rebel.Core.Domain;

public class SessionJournal
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public string UserId { get; init; } = default!;
    public string PlaybookName { get; set; } = default!;
    public string Instrument { get; set; } = default!;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public int DurationMinutes { get; set; }
    public int TradeCount { get; set; }
    public decimal RealizedPnl { get; set; }
    public int RuleViolations { get; set; }
    public decimal ApiCost { get; set; }
    public string? Summary { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
