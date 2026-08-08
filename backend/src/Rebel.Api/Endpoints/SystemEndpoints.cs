using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rebel.Api.Contracts;
using Rebel.Core.Abstractions;
using Rebel.Core.Market;
using Rebel.Core.Scoring;
using Rebel.Infrastructure.Ai;
using Rebel.Infrastructure.Configuration;
using Rebel.Infrastructure.MarketData;
using Rebel.Infrastructure.Persistence;
using Rebel.Infrastructure.Pipeline;

namespace Rebel.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
           .WithTags("System")
           .WithName("Health");

        app.MapGet("/api/status", (
            IMarketDataProvider marketData,
            INewsProvider news,
            IThesisGenerator thesis,
            ScoringEngine scoring,
            RebelDbContext db,
            IClock clock,
            IOptions<PipelineOptions> pipeline) =>
        {
            var synthetic = marketData.Name == SyntheticMarketDataProvider.ProviderName;

            return Results.Ok(new SystemStatusResponse(
                marketData.Name,
                news.Name,
                thesis.Model,
                synthetic,
                thesis.Model != TemplateThesisGenerator.ModelName,
                db.IsPostgres ? "postgres" : "sqlite",
                scoring.Weights.Version,
                scoring.Weights.ComputeHash(),
                pipeline.Value.EnableScheduledRuns,
                MarketCalendar.CurrentTradingDate(clock.UtcNow)));
        })
        .WithTags("System")
        .WithName("SystemStatus")
        .WithSummary("Which providers are live, and whether the data is real.");

        app.MapGet("/api/instruments", async (RebelDbContext db, CancellationToken ct) =>
        {
            var instruments = await db.Instruments
                .OrderBy(i => i.Symbol)
                .Select(i => new
                {
                    i.Symbol,
                    i.Name,
                    i.Sector,
                    i.SectorEtf,
                    i.Exchange,
                    i.IsBenchmark,
                    Bars = i.Bars.Count
                })
                .ToListAsync(ct);

            return Results.Ok(instruments);
        })
        .WithTags("System")
        .WithName("ListInstruments");

        app.MapPost("/api/instruments/sync", async (
            MarketDataIngestionService ingestion, CancellationToken ct) =>
        {
            var changed = await ingestion.SyncUniverseAsync(ct);
            return Results.Ok(new { changed });
        })
        .WithTags("System")
        .WithName("SyncUniverse");

        return app;
    }
}
