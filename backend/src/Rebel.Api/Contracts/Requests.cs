namespace Rebel.Api.Contracts;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string Email, string Username, string Password);
public record RefreshRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);

public record CreatePlaybookRequest(
    string Name,
    string Description,
    string AssetClass,
    List<string> Instruments,
    bool IsActive,
    PlaybookRulesDto Rules);

public record UpdatePlaybookRequest(
    string? Name,
    string? Description,
    string? AssetClass,
    List<string>? Instruments,
    PlaybookRulesDto? Rules);

public record PlaybookRulesDto(
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

public record ConnectProviderRequest(
    string ProviderType,
    string ProviderId,
    string ProviderName,
    Dictionary<string, string> Credentials,
    string KeySource);

public record PreflightRequest(string PlaybookId, string Instrument);
public record StartSessionRequest(string PlaybookId, string Instrument);
public record AskRequest(string Question);
