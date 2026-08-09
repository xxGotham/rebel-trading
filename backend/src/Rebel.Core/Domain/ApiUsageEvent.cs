namespace Rebel.Core.Domain;

public class ApiUsageEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserId { get; init; } = default!;
    public Guid? SessionId { get; init; }
    public string Provider { get; set; } = default!;
    public string? Model { get; set; }
    public string ServiceType { get; set; } = default!;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal AudioInputSeconds { get; set; }
    public decimal AudioOutputSeconds { get; set; }
    public int RequestCount { get; set; } = 1;
    public decimal ProviderCost { get; set; }
    public KeySource BillingSource { get; set; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
