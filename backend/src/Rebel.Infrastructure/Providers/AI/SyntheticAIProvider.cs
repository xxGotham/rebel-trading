using Rebel.Core.Abstractions;
using Rebel.Core.Trading;

namespace Rebel.Infrastructure.Providers.AI;

public class SyntheticAIProvider : IAIProvider
{
    private static readonly string[] Affirmations =
    [
        "Market conditions look stable. Stay disciplined and follow your Playbook.",
        "No significant setup detected yet. Be patient — your edge comes from waiting.",
        "Keep monitoring price action. Your entry conditions have not all been met.",
        "Volatility is low right now. Watch for a breakout before committing.",
        "Remember your risk management rules. One bad trade can erase multiple winners.",
    ];

    public string ProviderId => "synthetic";
    public string ProviderName => "Synthetic (Demo)";

    public Task<string> GenerateCoachingMessageAsync(AIContext context, TradingEvent trigger, CancellationToken ct = default)
    {
        var msg = trigger.Description;
        var pos = context.MarketState.CurrentPosition;
        if (pos != null)
            msg += $" You are {pos.Side} {pos.Quantity} {pos.Instrument} with unrealized P&L of {pos.UnrealizedPnl:C}.";
        return Task.FromResult(msg);
    }

    public Task<string> AnswerQuestionAsync(AIContext context, string question, CancellationToken ct = default)
    {
        var q = question.ToLowerInvariant();
        var pos = context.MarketState.CurrentPosition;

        var answer = q switch
        {
            var s when s.Contains("stop") =>
                $"Your stop is at {pos?.StopPrice?.ToString("F2") ?? "not set"}. " +
                $"Playbook rules: {RulesOrDefault(context.Playbook.Rules.StopLossRules)}.",
            var s when s.Contains("target") =>
                $"Your target is {pos?.TargetPrice?.ToString("F2") ?? "not set"}.",
            var s when (s.Contains("position") || s.Contains("p&l") || s.Contains("pnl")) =>
                pos != null
                    ? $"You are {pos.Side} {pos.Quantity} {pos.Instrument} at {pos.EntryPrice:F2}. Unrealized P&L: {pos.UnrealizedPnl:C}."
                    : "You have no open position right now.",
            var s when (s.Contains("entry") || s.Contains("setup")) =>
                $"Entry conditions: {RulesOrDefault(context.Playbook.Rules.EntryConditions)}.",
            _ => Affirmations[Random.Shared.Next(Affirmations.Length)],
        };

        return Task.FromResult(answer);
    }

    private static string RulesOrDefault(List<string> rules) =>
        rules.Count == 0 ? "none defined" : string.Join("; ", rules.Take(3));
}
