using Microsoft.EntityFrameworkCore;
using Rebel.Api.Contracts;
using Rebel.Core.Domain;
using Rebel.Core.Performance;
using Rebel.Infrastructure.Persistence;
using Rebel.Infrastructure.Pipeline;

namespace Rebel.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recommendations").WithTags("Recommendations");

        group.MapGet("/", async (
            RebelDbContext db,
            DateOnly? from,
            DateOnly? to,
            string? strategy,
            int? limit,
            CancellationToken ct) =>
        {
            var query = db.Recommendations
                .Include(r => r.Instrument)
                .Include(r => r.Outcome)
                .AsQueryable();

            if (from is not null) query = query.Where(r => r.TradingDate >= from.Value);
            if (to is not null) query = query.Where(r => r.TradingDate <= to.Value);
            if (!string.IsNullOrWhiteSpace(strategy)) query = query.Where(r => r.Strategy == strategy);

            var results = await query
                .OrderByDescending(r => r.TradingDate)
                .ThenBy(r => r.Rank)
                .Take(Math.Clamp(limit ?? 50, 1, 500))
                .ToListAsync(ct);

            return Results.Ok(results.Select(RecommendationMapper.ToResponse));
        })
        .WithName("ListRecommendations");

        group.MapGet("/{id:guid}", async (Guid id, RebelDbContext db, CancellationToken ct) =>
        {
            var rec = await db.Recommendations
                .Include(r => r.Instrument)
                .Include(r => r.Outcome)
                .FirstOrDefaultAsync(r => r.Id == id, ct);

            return rec is null ? Results.NotFound() : Results.Ok(RecommendationMapper.ToResponse(rec));
        })
        .WithName("GetRecommendation");

        // The raw signal payload the model was shown, verbatim. This is the
        // "show your work" endpoint — it is what makes a thesis auditable
        // instead of something the user has to take on faith.
        group.MapGet("/{id:guid}/evidence", async (Guid id, RebelDbContext db, CancellationToken ct) =>
        {
            var rec = await db.Recommendations.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (rec is null) return Results.NotFound();

            return Results.Ok(new
            {
                rec.Symbol,
                rec.TradingDate,
                rec.Model,
                rec.PromptVersion,
                Signals = rec.SignalsJson,
                News = rec.NewsJson,
                ModelResponse = rec.LlmRawJson,
                rec.ValidationNotes
            });
        })
        .WithName("GetRecommendationEvidence");

        return app;
    }

    public static IEndpointRouteBuilder MapPerformanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/performance").WithTags("Performance");

        group.MapGet("/", async (RebelDbContext db, DateOnly? from, CancellationToken ct) =>
        {
            var trades = await LoadTradesAsync(db, from, ct);

            return Results.Ok(new
            {
                Overall = PerformanceStats.From(trades),
                ByStrategy = PerformanceStats.Breakdown(trades, t => t.Strategy),
                ByConfidence = PerformanceStats.Breakdown(trades, t => t.Confidence, c => c.ToString()),
                ByOutcome = PerformanceStats.Breakdown(trades, t => t.Status, s => s.ToString()),
                ByHoldingPeriod = PerformanceStats.Breakdown(trades, t => Bucket(t.DaysHeld))
            });
        })
        .WithName("GetPerformance");

        group.MapPost("/grade", async (GradingService grading, CancellationToken ct) =>
        {
            var summary = await grading.GradeOutstandingAsync(ct);
            return Results.Ok(new { summary.Examined, summary.Graded, summary.StillOpen });
        })
        .WithName("GradeRecommendations")
        .WithSummary("Grade every unresolved recommendation against subsequent price action.");

        return app;
    }

    private static async Task<IReadOnlyList<GradedTrade>> LoadTradesAsync(
        RebelDbContext db, DateOnly? from, CancellationToken ct)
    {
        var query = db.Recommendations.Include(r => r.Outcome).Where(r => r.Outcome != null);
        if (from is not null) query = query.Where(r => r.TradingDate >= from.Value);

        var rows = await query.ToListAsync(ct);

        return rows
            .Where(r => r.Outcome!.Status != OutcomeStatus.Open)
            .Select(r => new GradedTrade
            {
                Symbol = r.Symbol,
                Strategy = string.IsNullOrWhiteSpace(r.Strategy) ? "unclassified" : r.Strategy,
                Confidence = r.Confidence,
                Status = r.Outcome!.Status,
                TradingDate = r.TradingDate,
                ReturnPercent = r.Outcome.ReturnPercent ?? 0m,
                RMultiple = r.Outcome.RMultiple,
                DaysHeld = r.Outcome.DaysHeld ?? 0,
                BenchmarkReturnPercent = r.Outcome.BenchmarkReturnPercent
            })
            .ToArray();
    }

    private static string Bucket(int daysHeld) => daysHeld switch
    {
        <= 1 => "1 day",
        <= 3 => "2-3 days",
        <= 5 => "4-5 days",
        _ => "6+ days"
    };
}
