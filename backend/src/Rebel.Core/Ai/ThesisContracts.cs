using Rebel.Core.Domain;

namespace Rebel.Core.Ai;

public sealed record ThesisNewsItem
{
    public required string Id { get; init; }
    public required string Headline { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public DateTime PublishedAtUtc { get; init; }
}

/// <summary>
/// One candidate exactly as the model sees it. Everything numeric here was
/// computed by <c>Rebel.Core</c>; the model's job is to interpret, not extend.
/// </summary>
public sealed record ThesisCandidate
{
    public required string Symbol { get; init; }
    public required string CompanyName { get; init; }
    public required string Sector { get; init; }
    public required string Strategy { get; init; }
    public required int Rank { get; init; }
    public required double Score { get; init; }
    public required string Confidence { get; init; }

    public required decimal LastPrice { get; init; }
    public required decimal PreviousClose { get; init; }
    public required double GapPercent { get; init; }
    public required double RelativeVolume { get; init; }
    public required double Rsi14 { get; init; }
    public required double Atr14 { get; init; }
    public required double AtrPercent { get; init; }
    public required double Sma20 { get; init; }
    public required double Sma50 { get; init; }
    public required double Momentum20Percent { get; init; }
    public required double PercentOff20DayHigh { get; init; }
    public required double RealizedVolatility { get; init; }
    public required double RelativeStrengthVsMarket { get; init; }
    public required double SectorStrength { get; init; }
    public required long AverageVolume20 { get; init; }

    public required decimal EntryLow { get; init; }
    public required decimal EntryHigh { get; init; }
    public required decimal Target { get; init; }
    public required decimal StopLoss { get; init; }
    public required decimal RiskRewardRatio { get; init; }
    public required int HoldingPeriodDays { get; init; }

    public IReadOnlyList<ThesisNewsItem> News { get; init; } = [];
}

public sealed record ThesisRequest
{
    public required DateOnly TradingDate { get; init; }
    public required IReadOnlyList<ThesisCandidate> Candidates { get; init; }
    public required int MaxSelections { get; init; }

    /// <summary>Market-wide context (index momentum, breadth) so theses aren't written in a vacuum.</summary>
    public string MarketContext { get; init; } = string.Empty;
}

/// <summary>
/// The model's contribution to a recommendation: prose, and a yes/no on whether
/// the candidate is worth showing. No prices, no targets, no sizing.
/// </summary>
public sealed record ThesisSelection
{
    public required string Symbol { get; init; }
    public required bool Selected { get; init; }
    public string Thesis { get; init; } = string.Empty;
    public string BullCase { get; init; } = string.Empty;
    public string BearCase { get; init; } = string.Empty;
    public string Risks { get; init; } = string.Empty;
    public string CatalystSummary { get; init; } = string.Empty;
    public string WhyThisOne { get; init; } = string.Empty;

    public IEnumerable<(string Field, string Text)> ProseFields()
    {
        yield return (nameof(Thesis), Thesis);
        yield return (nameof(BullCase), BullCase);
        yield return (nameof(BearCase), BearCase);
        yield return (nameof(Risks), Risks);
        yield return (nameof(CatalystSummary), CatalystSummary);
        yield return (nameof(WhyThisOne), WhyThisOne);
    }
}

public sealed record ThesisResponse
{
    public IReadOnlyList<ThesisSelection> Selections { get; init; } = [];
    public string Model { get; init; } = string.Empty;
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public string RawJson { get; init; } = "{}";
}

/// <summary>Maps a composite score onto the confidence shown to the user.</summary>
public static class ConfidenceScale
{
    public static ConfidenceLevel FromScore(double score) => score switch
    {
        >= 80 => ConfidenceLevel.High,
        >= 65 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.Low
    };
}
