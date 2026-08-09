namespace Rebel.Core.Abstractions;

public record AIContext(
    string UserName,
    Domain.Playbook Playbook,
    Trading.MarketState MarketState,
    IReadOnlyList<Trading.TradingEvent> RecentEvents,
    IReadOnlyList<Domain.ConversationMessage> RecentMessages);

public interface IAIProvider
{
    string ProviderId { get; }
    string ProviderName { get; }
    Task<string> GenerateCoachingMessageAsync(AIContext context, Trading.TradingEvent trigger, CancellationToken ct = default);
    Task<string> AnswerQuestionAsync(AIContext context, string question, CancellationToken ct = default);
}
