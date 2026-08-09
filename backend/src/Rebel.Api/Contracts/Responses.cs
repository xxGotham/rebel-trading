namespace Rebel.Api.Contracts;

public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(
    string Id,
    string Email,
    string Username,
    string SubscriptionPlan,
    List<string> Entitlements);

public record PlaybookResponse(
    string Id,
    string Name,
    string Description,
    string AssetClass,
    List<string> Instruments,
    bool IsActive,
    int Version,
    string CreatedAt,
    string UpdatedAt,
    PlaybookRulesResponse Rules);

public record PlaybookRulesResponse(
    string? TradingHoursStart,
    string? TradingHoursEnd,
    List<string> EntryConditions,
    List<string> ExitConditions,
    decimal? RiskPerTrade,
    decimal? MaxDailyRisk,
    List<string> StopLossRules,
    List<string> StopManagementRules,
    List<string> ProfitTargets,
    List<string> TrailingRules,
    List<string> PositionSizingRules,
    int? MaxTradesPerDay,
    List<string> AdditionalConditions);

public record ProviderConnectionResponse(
    string ProviderId,
    string ProviderName,
    string ProviderType,
    string Status,
    string KeySource);

public record PreflightResponse(
    bool IsAuthenticated,
    bool HasActiveSubscription,
    bool HasLiveCoachingEntitlement,
    bool HasActivePlaybook,
    bool IsMarketDataConnected,
    bool IsAIConnected,
    bool IsBrokerConnected);

public record SessionResponse(
    string Id,
    string PlaybookId,
    string PlaybookName,
    string Instrument,
    string AssetClass,
    string Status,
    string StartedAt,
    string? EndedAt,
    decimal SessionPnl,
    int TradeCount);

public record SessionSummaryResponse(
    string Id,
    string PlaybookName,
    string Instrument,
    string StartedAt,
    string EndedAt,
    int DurationMinutes,
    int TradeCount,
    decimal RealizedPnl,
    int RuleViolations,
    decimal ApiCost);
