using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rebel.Api.Contracts;
using Rebel.Core.Domain;
using Rebel.Infrastructure.Persistence;
using Rebel.Infrastructure.Pipeline;

namespace Rebel.Api.Endpoints;

public sealed record StartRunRequest(DateOnly? TradingDate, bool? RefreshMarketData);

public static class RunEndpoints
{
    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/runs").WithTags("Runs");

        // Synchronous by design in V1: one user, ~130 symbols, a few seconds of
        // work. The scheduled pre-market job calls the same pipeline through
        // Hangfire, so nothing has to change when this needs to go async.
        group.MapPost("/", async (
            StartRunRequest? request,
            AnalysisPipeline pipeline,
            CancellationToken ct) =>
        {
            var run = await pipeline.RunAsync(new RunRequest
            {
                TradingDate = request?.TradingDate,
                RefreshMarketData = request?.RefreshMarketData ?? true,
                Trigger = RunTrigger.Manual
            }, ct);

            return run.Status == RunStatus.Failed
                ? Results.Problem(
                    title: "Analysis run failed",
                    detail: run.ErrorMessage,
                    statusCode: StatusCodes.Status500InternalServerError,
                    extensions: new Dictionary<string, object?> { ["runId"] = run.Id })
                : Results.Ok(ResponseMapper.ToSummary(run));
        })
        .WithName("StartRun")
        .WithSummary("Run the full analysis pipeline and return the resulting run.");

        group.MapGet("/", async (RebelDbContext db, int? limit, CancellationToken ct) =>
        {
            var runs = await db.AnalysisRuns
                .OrderByDescending(r => r.StartedAtUtc)
                .Take(Math.Clamp(limit ?? 25, 1, 200))
                .ToListAsync(ct);

            return Results.Ok(runs.Select(ResponseMapper.ToSummary));
        })
        .WithName("ListRuns");

        group.MapGet("/latest", async (RebelDbContext db, CancellationToken ct) =>
        {
            var run = await db.AnalysisRuns
                .OrderByDescending(r => r.StartedAtUtc)
                .FirstOrDefaultAsync(ct);

            return run is null ? Results.NoContent() : await BuildDetailAsync(db, run, ct);
        })
        .WithName("LatestRun");

        group.MapGet("/{id:guid}", async (Guid id, RebelDbContext db, CancellationToken ct) =>
        {
            var run = await db.AnalysisRuns.FirstOrDefaultAsync(r => r.Id == id, ct);
            return run is null ? Results.NotFound() : await BuildDetailAsync(db, run, ct);
        })
        .WithName("GetRun");

        return app;
    }

    private static async Task<IResult> BuildDetailAsync(RebelDbContext db, AnalysisRun run, CancellationToken ct)
    {
        var recommendations = await db.Recommendations
            .Include(r => r.Instrument)
            .Include(r => r.Outcome)
            .Where(r => r.AnalysisRunId == run.Id)
            .OrderBy(r => r.Rank)
            .ToListAsync(ct);

        var candidates = await db.Candidates
            .Include(c => c.Instrument)
            .Where(c => c.AnalysisRunId == run.Id)
            .OrderBy(c => c.ExclusionReason == null ? 0 : 1)
            .ThenBy(c => c.Rank)
            .ToListAsync(ct);

        return Results.Ok(new RunDetailResponse(
            ResponseMapper.ToSummary(run),
            run.StageLog.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            recommendations.Select(RecommendationMapper.ToResponse).ToArray(),
            candidates.Select(ResponseMapper.ToResponse).ToArray()));
    }
}

public static class RecommendationMapper
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static RecommendationResponse ToResponse(Recommendation r) => new(
        r.Id,
        r.AnalysisRunId,
        r.Symbol,
        r.Instrument?.Name,
        r.Instrument?.Sector,
        r.TradingDate,
        r.Direction.ToString(),
        r.Strategy,
        r.Confidence.ToString(),
        r.Score,
        r.Rank,
        r.ReferencePrice,
        r.EntryLow,
        r.EntryHigh,
        r.Target,
        r.StopLoss,
        r.RiskRewardRatio,
        r.StopDistancePercent,
        r.TargetDistancePercent,
        r.SuggestedShares,
        r.SuggestedRiskAmount,
        r.HoldingPeriodDays,
        r.Thesis,
        r.BullCase,
        r.BearCase,
        r.Risks,
        r.CatalystSummary,
        r.WhyThisOne,
        r.Model,
        r.PromptVersion,
        r.ValidationNotes,
        ParseNews(r.NewsJson),
        r.Outcome is null ? null : ResponseMapper.ToResponse(r.Outcome));

    private static IReadOnlyList<NewsResponse> ParseNews(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return [];

        try
        {
            return JsonSerializer.Deserialize<List<NewsResponse>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            // Stored payloads outlive schema changes; a stale shape should not
            // take down the whole recommendation view.
            return [];
        }
    }
}
