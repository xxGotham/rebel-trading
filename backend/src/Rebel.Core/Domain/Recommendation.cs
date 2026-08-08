namespace Rebel.Core.Domain;

/// <summary>
/// A recommendation as shown to the user.
/// <para>
/// Split by authorship, and the split is enforced in code:
/// every numeric field is computed by <c>Rebel.Core</c>, and the LLM contributes
/// only the prose fields. If the model emits a number that was not in its input
/// payload the response is rejected — see <c>NumberGuard</c>.
/// </para>
/// </summary>
public class Recommendation
{
    public Guid Id { get; set; }
    public Guid AnalysisRunId { get; set; }
    public AnalysisRun? AnalysisRun { get; set; }

    public int InstrumentId { get; set; }
    public Instrument? Instrument { get; set; }
    public string Symbol { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateOnly TradingDate { get; set; }
    public TradeDirection Direction { get; set; } = TradeDirection.Long;

    // ---- App-computed: prices and risk ----
    public decimal ReferencePrice { get; set; }
    public decimal EntryLow { get; set; }
    public decimal EntryHigh { get; set; }
    public decimal Target { get; set; }
    public decimal StopLoss { get; set; }
    public decimal RiskRewardRatio { get; set; }
    public decimal StopDistancePercent { get; set; }
    public decimal TargetDistancePercent { get; set; }
    public int SuggestedShares { get; set; }
    public decimal SuggestedRiskAmount { get; set; }
    public int HoldingPeriodDays { get; set; }

    // ---- App-computed: conviction ----
    public double Score { get; set; }
    public int Rank { get; set; }
    public ConfidenceLevel Confidence { get; set; }
    public string Strategy { get; set; } = string.Empty;

    // ---- LLM-authored: prose only ----
    public string Thesis { get; set; } = string.Empty;
    public string BullCase { get; set; } = string.Empty;
    public string BearCase { get; set; } = string.Empty;
    public string Risks { get; set; } = string.Empty;
    public string CatalystSummary { get; set; } = string.Empty;
    public string WhyThisOne { get; set; } = string.Empty;

    // ---- Provenance ----
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string LlmRawJson { get; set; } = "{}";

    /// <summary>Signals shown to the model, frozen at generation time.</summary>
    public string SignalsJson { get; set; } = "{}";

    /// <summary>Headlines cited in the payload: id, headline, source, url, published.</summary>
    public string NewsJson { get; set; } = "[]";

    /// <summary>Non-fatal validator notes (e.g. a repaired field), for later review.</summary>
    public string? ValidationNotes { get; set; }

    public RecommendationOutcome? Outcome { get; set; }
}
