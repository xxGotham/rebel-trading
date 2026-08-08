using Rebel.Core.Abstractions;
using Rebel.Core.Ai;

namespace Rebel.Infrastructure.Ai;

/// <summary>
/// Writes the prose deterministically from the same signals an LLM would see,
/// using qualitative language only.
/// <para>
/// Two reasons this exists. First, it lets the entire pipeline be run, tested
/// and demonstrated with no API key and no cost. Second, it is the honest
/// baseline: if recommendations written by this class perform the same as ones
/// written by the model, the model is adding presentation, not edge — and the
/// performance tables are set up to answer exactly that question, since the
/// generator is recorded per recommendation.
/// </para>
/// </summary>
public sealed class TemplateThesisGenerator : IThesisGenerator
{
    public const string ModelName = "template-v1";

    public string Model => ModelName;
    public string PromptVersion => "template-v1";

    public Task<ThesisResponse> GenerateAsync(ThesisRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var selections = request.Candidates
            .Select((c, index) => Write(c, index < request.MaxSelections))
            .ToArray();

        return Task.FromResult(new ThesisResponse
        {
            Selections = selections,
            Model = ModelName,
            RawJson = "{\"generator\":\"template\"}"
        });
    }

    private static ThesisSelection Write(ThesisCandidate c, bool selected)
    {
        var trend = c.Momentum20Percent switch
        {
            > 12 => "a strong uptrend",
            > 4 => "a steady uptrend",
            > 0 => "a mild upward drift",
            _ => "a flat-to-soft tape"
        };

        var position = c.PercentOff20DayHigh switch
        {
            < 1 => "pressing right against its recent high",
            < 4 => "just under its recent high",
            < 10 => "consolidating below its recent high",
            _ => "well off its recent high"
        };

        var volume = c.RelativeVolume switch
        {
            > 0.15 => "Pre-market participation is heavy for this hour",
            > 0.08 => "Pre-market participation is above what this name usually sees",
            > 0.03 => "Pre-market participation is around normal",
            _ => "Pre-market participation is light"
        };

        var rsi = c.Rsi14 switch
        {
            > 70 => "Momentum is stretched, which cuts both ways: it confirms strength but leaves less room before a pullback",
            > 55 => "Momentum is firm without being overextended",
            > 45 => "Momentum is neutral",
            _ => "Momentum is soft"
        };

        var relative = c.RelativeStrengthVsMarket > 0
            ? "It has been outpacing the broad market"
            : "It has been lagging the broad market";

        var sector = c.SectorStrength > 0
            ? $"and the {c.Sector} group is working"
            : $"while the {c.Sector} group has been a headwind";

        var hasNews = c.News.Count > 0;
        var catalyst = hasNews
            ? $"Overnight coverage: {string.Join(" | ", c.News.Take(3).Select(n => n.Headline))}"
            : "No overnight headlines for this name. The case here is technical, not news-driven.";

        return new ThesisSelection
        {
            Symbol = c.Symbol,
            Selected = selected,
            Thesis =
                $"{c.CompanyName} is in {trend} and is {position}. {relative}, {sector}. " +
                $"The screen flagged it under {c.Strategy}.",
            BullCase =
                $"{volume}. {rsi}. If it holds above its short-term average and the group " +
                "stays bid, the path toward the target is the continuation of a trend that " +
                "is already in place rather than a reversal that has to be called correctly.",
            BearCase =
                "This is a trend-following entry, so it is buying strength that has already " +
                "been paid for. If the broad market turns or the group rotates out, this name " +
                "gives back the recent move faster than it made it. Nothing in the setup " +
                "argues the stock is cheap.",
            Risks =
                "The setup is invalidated on a close back below the short-term moving average " +
                (hasNews
                    ? "or if the overnight story is faded during regular hours."
                    : "or on any adverse headline, since there is no news anchoring the move."),
            CatalystSummary = catalyst,
            WhyThisOne =
                $"Of the names on this list it ranked highest on the {c.Strategy} profile, " +
                "combining trend, relative strength and participation rather than leaning on any one of them."
        };
    }
}
