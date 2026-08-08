using Rebel.Core.Domain;

namespace Rebel.Api.Contracts;

public sealed record RunSummaryResponse(
    Guid Id,
    DateOnly TradingDate,
    string Status,
    string Trigger,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    double? DurationSeconds,
    int UniverseSize,
    int CandidateCount,
    int RecommendationCount,
    string ScoringVersion,
    string PromptVersion,
    string ConfigHash,
    string? Model,
    int PromptTokens,
    int CompletionTokens,
    string? ErrorMessage);

public sealed record CandidateResponse(
    string Symbol,
    string? Name,
    int Rank,
    double Score,
    bool Selected,
    string? ExclusionReason,
    decimal LastPrice,
    double GapPercent,
    double RelativeVolume,
    double Rsi14,
    double AtrPercent,
    double Momentum20,
    double PercentOff20DayHigh,
    double RelativeStrengthVsMarket,
    double SectorStrength,
    int NewsCount);

public sealed record NewsResponse(string Id, string Headline, string Source, string Url, DateTime PublishedAtUtc);

public sealed record OutcomeResponse(
    string Status,
    DateOnly? EntryDate,
    decimal? EntryPrice,
    DateOnly? ExitDate,
    decimal? ExitPrice,
    decimal? ReturnPercent,
    decimal? RMultiple,
    decimal? MaxFavorablePercent,
    decimal? MaxAdversePercent,
    int? DaysHeld,
    decimal? BenchmarkReturnPercent,
    DateTime? GradedAtUtc);

public sealed record RecommendationResponse(
    Guid Id,
    Guid RunId,
    string Symbol,
    string? Name,
    string? Sector,
    DateOnly TradingDate,
    string Direction,
    string Strategy,
    string Confidence,
    double Score,
    int Rank,
    decimal ReferencePrice,
    decimal EntryLow,
    decimal EntryHigh,
    decimal Target,
    decimal StopLoss,
    decimal RiskRewardRatio,
    decimal StopDistancePercent,
    decimal TargetDistancePercent,
    int SuggestedShares,
    decimal SuggestedRiskAmount,
    int HoldingPeriodDays,
    string Thesis,
    string BullCase,
    string BearCase,
    string Risks,
    string CatalystSummary,
    string WhyThisOne,
    string Model,
    string PromptVersion,
    string? ValidationNotes,
    IReadOnlyList<NewsResponse> News,
    OutcomeResponse? Outcome);

public sealed record RunDetailResponse(
    RunSummaryResponse Run,
    IReadOnlyList<string> StageLog,
    IReadOnlyList<RecommendationResponse> Recommendations,
    IReadOnlyList<CandidateResponse> Candidates);

public sealed record PaperPositionResponse(
    Guid Id,
    Guid RecommendationId,
    string Symbol,
    string Status,
    DateTime OpenedAtUtc,
    decimal EntryPrice,
    int Shares,
    decimal StopLoss,
    decimal Target,
    decimal CostBasis,
    DateTime? ClosedAtUtc,
    decimal? ExitPrice,
    string? ExitReason,
    decimal? RealizedPnl,
    decimal? RealizedPercent);

public sealed record PortfolioResponse(
    int OpenPositions,
    int ClosedPositions,
    decimal DeployedCapital,
    decimal RealizedPnl,
    decimal RealizedPercent,
    IReadOnlyList<PaperPositionResponse> Positions);

/// <summary>
/// Surfaces which providers are actually in use. The UI shows this prominently:
/// a user must never mistake a synthetic-data run for a real one.
/// </summary>
public sealed record SystemStatusResponse(
    string MarketDataProvider,
    string NewsProvider,
    string ThesisModel,
    bool UsingSyntheticData,
    bool UsingLlm,
    string Database,
    string ScoringVersion,
    string ConfigHash,
    bool ScheduledRunsEnabled,
    DateOnly NextTradingDate);

public static class ResponseMapper
{
    public static RunSummaryResponse ToSummary(AnalysisRun run) => new(
        run.Id,
        run.TradingDate,
        run.Status.ToString(),
        run.Trigger.ToString(),
        run.StartedAtUtc,
        run.FinishedAtUtc,
        run.FinishedAtUtc is null ? null : (run.FinishedAtUtc.Value - run.StartedAtUtc).TotalSeconds,
        run.UniverseSize,
        run.CandidateCount,
        run.RecommendationCount,
        run.ScoringVersion,
        run.PromptVersion,
        run.ConfigHash,
        run.Model,
        run.PromptTokens,
        run.CompletionTokens,
        run.ErrorMessage);

    public static CandidateResponse ToResponse(Candidate c) => new(
        c.Symbol,
        c.Instrument?.Name,
        c.Rank,
        c.Score,
        c.Selected,
        c.ExclusionReason,
        c.LastPrice,
        c.GapPercent,
        c.RelativeVolume,
        c.Rsi14,
        c.AtrPercent,
        c.Momentum20,
        c.PercentOff20DayHigh,
        c.RelativeStrengthVsMarket,
        c.SectorStrength,
        c.NewsCount);

    public static OutcomeResponse ToResponse(RecommendationOutcome o) => new(
        o.Status.ToString(),
        o.EntryDate,
        o.EntryPrice,
        o.ExitDate,
        o.ExitPrice,
        o.ReturnPercent,
        o.RMultiple,
        o.MaxFavorablePercent,
        o.MaxAdversePercent,
        o.DaysHeld,
        o.BenchmarkReturnPercent,
        o.GradedAtUtc);

    public static PaperPositionResponse ToResponse(PaperPosition p) => new(
        p.Id,
        p.RecommendationId,
        p.Symbol,
        p.Status.ToString(),
        p.OpenedAtUtc,
        p.EntryPrice,
        p.Shares,
        p.StopLoss,
        p.Target,
        p.CostBasis,
        p.ClosedAtUtc,
        p.ExitPrice,
        p.ExitReason,
        p.RealizedPnl,
        p.RealizedPnl is null || p.CostBasis == 0m
            ? null
            : Math.Round(p.RealizedPnl.Value / p.CostBasis * 100m, 2));
}
