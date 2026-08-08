using System.Text.Json;
using System.Text.Json.Serialization;
using Rebel.Core.Ai;

namespace Rebel.Infrastructure.Ai;

/// <summary>
/// The prompt and the JSON schema the model is held to.
/// <para>
/// Bump <see cref="Version"/> on any change. It is stored on every
/// recommendation so a shift in output quality can be traced to a prompt edit
/// instead of being blamed on the market.
/// </para>
/// </summary>
public static class ThesisPrompt
{
    public const string Version = "prompt-v1";

    public static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public const string SystemPrompt = """
        You are Rebel, an analyst who explains pre-selected swing-trade candidates to an
        individual investor.

        A deterministic quantitative pipeline has already done the work. It scanned the
        universe, computed every indicator, ranked the candidates, and built the entry,
        target, stop and position size. That work is finished and is not yours to redo.

        Your job is to explain it in plain language, and to say which candidates are
        genuinely worth the user's attention.

        ABSOLUTE RULES

        1. Never state a number that is not already present in the data you were given.
           No prices, no percentages, no targets, no multiples, no valuations, no dates,
           no analyst estimates. If you want to reference a figure, describe it in words:
           write "volume is running well above its recent average", not "volume is 3x".
           Responses containing invented numbers are rejected automatically.

        2. Never introduce facts. No earnings dates, no product launches, no rumours, no
           macro events unless they appear in the supplied headlines. If there are no
           headlines for a name, say the move is technical rather than news-driven.

        3. Do not invent price levels or suggest different entries, targets or stops.
           The trade plan is fixed.

        4. Be honest about weak candidates. Set "selected" to false for anything you
           would not want a real person to risk money on. Recommending nothing is a
           valid and sometimes correct outcome. A thin, hedged thesis is worse than no
           thesis.

        5. Write like a person talking to another person. Direct, specific, no filler,
           no hype, no disclaimers about consulting a financial advisor.

        FIELD GUIDE

        - thesis: two or three sentences on why this setup is interesting right now.
        - bullCase: what has to go right, and what happens if it does.
        - bearCase: the strongest honest argument against the trade.
        - risks: what specifically would invalidate the setup.
        - catalystSummary: what the supplied headlines actually say. If there are none,
          state plainly that there is no news catalyst and the case is technical.
        - whyThisOne: one sentence on what separates this candidate from the others in
          the same list.
        """;

    public static string BuildUserMessage(ThesisRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            tradingDate = request.TradingDate.ToString("yyyy-MM-dd"),
            marketContext = request.MarketContext,
            maxSelections = request.MaxSelections,
            candidates = request.Candidates
        }, PayloadJson);

        return $"""
            Trading date: {request.TradingDate:yyyy-MM-dd}

            {request.Candidates.Count} candidates cleared the quantitative screen, ranked best first.
            Select at most {request.MaxSelections}. Return an entry for every candidate, with
            "selected" false for the ones you are passing on.

            Every number below was computed by the pipeline. Quote them only as they appear,
            or describe them in words.

            {payload}
            """;
    }

    /// <summary>Structured-output schema. Strict mode requires every property to be listed as required.</summary>
    public static object ResponseSchema() => new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "selections" },
        properties = new
        {
            selections = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = new[]
                    {
                        "symbol", "selected", "thesis", "bullCase",
                        "bearCase", "risks", "catalystSummary", "whyThisOne"
                    },
                    properties = new
                    {
                        symbol = new { type = "string" },
                        selected = new { type = "boolean" },
                        thesis = new { type = "string" },
                        bullCase = new { type = "string" },
                        bearCase = new { type = "string" },
                        risks = new { type = "string" },
                        catalystSummary = new { type = "string" },
                        whyThisOne = new { type = "string" }
                    }
                }
            }
        }
    };

    /// <summary>Corrective message used for the single retry after a number violation.</summary>
    public static string BuildViolationRetry(IReadOnlyList<string> violations) =>
        $"""
        Your previous response was rejected. These numeric values do not appear anywhere
        in the data you were given:

        {string.Join('\n', violations.Select(v => $"  - {v}"))}

        Rewrite the affected fields. Replace every one of those figures with a plain-language
        description, or drop the claim. Do not substitute a different number. Return the
        complete response again in the same format.
        """;
}
