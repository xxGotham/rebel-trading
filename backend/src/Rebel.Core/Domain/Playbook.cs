namespace Rebel.Core.Domain;

public class Playbook
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserId { get; init; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = string.Empty;
    public AssetClass AssetClass { get; set; }
    public List<string> Instruments { get; set; } = new();
    public bool IsActive { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public PlaybookRules Rules { get; set; } = new();
}

public class PlaybookRules
{
    public string? TradingHoursStart { get; set; }
    public string? TradingHoursEnd { get; set; }
    public List<string> EntryConditions { get; set; } = new();
    public List<string> ExitConditions { get; set; } = new();
    public decimal? RiskPerTrade { get; set; }
    public decimal? MaxDailyRisk { get; set; }
    public List<string> StopLossRules { get; set; } = new();
    public List<string> StopManagementRules { get; set; } = new();
    public List<string> ProfitTargets { get; set; } = new();
    public List<string> TrailingRules { get; set; } = new();
    public List<string> PositionSizingRules { get; set; } = new();
    public int? MaxTradesPerDay { get; set; }
    public List<string> AdditionalConditions { get; set; } = new();
}
