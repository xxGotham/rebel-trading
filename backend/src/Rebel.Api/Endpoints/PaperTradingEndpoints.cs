using Microsoft.EntityFrameworkCore;
using Rebel.Api.Contracts;
using Rebel.Core.Abstractions;
using Rebel.Core.Domain;
using Rebel.Infrastructure.Persistence;

namespace Rebel.Api.Endpoints;

public sealed record OpenPositionRequest(Guid RecommendationId, int? Shares, decimal? EntryPrice);
public sealed record ClosePositionRequest(decimal? ExitPrice, string? Reason);

public static class PaperTradingEndpoints
{
    public static IEndpointRouteBuilder MapPaperTradingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/paper").WithTags("Paper Trading");

        group.MapPost("/positions", async (
            OpenPositionRequest request,
            RebelDbContext db,
            IClock clock,
            CancellationToken ct) =>
        {
            var rec = await db.Recommendations.FirstOrDefaultAsync(r => r.Id == request.RecommendationId, ct);
            if (rec is null) return Results.NotFound(new { error = "Recommendation not found." });

            var existing = await db.PaperPositions
                .FirstOrDefaultAsync(p => p.RecommendationId == rec.Id, ct);
            if (existing is not null)
                return Results.Conflict(new { error = "This recommendation is already in the portfolio." });

            var shares = request.Shares ?? rec.SuggestedShares;
            if (shares <= 0) return Results.BadRequest(new { error = "Shares must be greater than zero." });

            // Defaults to the midpoint of the entry band; overriding it is how a
            // user records the price they would actually have paid.
            var entryPrice = request.EntryPrice ?? Math.Round((rec.EntryLow + rec.EntryHigh) / 2m, 2);
            if (entryPrice <= 0m) return Results.BadRequest(new { error = "Entry price must be greater than zero." });

            var position = new PaperPosition
            {
                Id = Guid.NewGuid(),
                RecommendationId = rec.Id,
                InstrumentId = rec.InstrumentId,
                Symbol = rec.Symbol,
                Status = PaperPositionStatus.Open,
                Direction = rec.Direction,
                OpenedAtUtc = clock.UtcNow,
                EntryPrice = entryPrice,
                Shares = shares,
                StopLoss = rec.StopLoss,
                Target = rec.Target
            };

            db.PaperPositions.Add(position);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/paper/positions/{position.Id}", ResponseMapper.ToResponse(position));
        })
        .WithName("OpenPaperPosition")
        .WithSummary("Accept a recommendation into the simulated portfolio.");

        group.MapPost("/positions/{id:guid}/close", async (
            Guid id,
            ClosePositionRequest request,
            RebelDbContext db,
            IClock clock,
            CancellationToken ct) =>
        {
            var position = await db.PaperPositions.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (position is null) return Results.NotFound();
            if (position.Status == PaperPositionStatus.Closed)
                return Results.Conflict(new { error = "Position is already closed." });

            var exitPrice = request.ExitPrice ?? await LastCloseAsync(db, position.InstrumentId, ct);
            if (exitPrice is null or <= 0m)
                return Results.BadRequest(new { error = "No exit price supplied and no recent close available." });

            position.Status = PaperPositionStatus.Closed;
            position.ClosedAtUtc = clock.UtcNow;
            position.ExitPrice = exitPrice;
            position.ExitReason = request.Reason ?? "manual";

            await db.SaveChangesAsync(ct);
            return Results.Ok(ResponseMapper.ToResponse(position));
        })
        .WithName("ClosePaperPosition");

        group.MapGet("/positions", async (RebelDbContext db, string? status, CancellationToken ct) =>
        {
            var query = db.PaperPositions.AsQueryable();

            if (Enum.TryParse<PaperPositionStatus>(status, ignoreCase: true, out var parsed))
                query = query.Where(p => p.Status == parsed);

            var positions = await query.OrderByDescending(p => p.OpenedAtUtc).ToListAsync(ct);
            return Results.Ok(positions.Select(ResponseMapper.ToResponse));
        })
        .WithName("ListPaperPositions");

        group.MapGet("/portfolio", async (RebelDbContext db, CancellationToken ct) =>
        {
            var positions = await db.PaperPositions.OrderByDescending(p => p.OpenedAtUtc).ToListAsync(ct);

            var open = positions.Where(p => p.Status == PaperPositionStatus.Open).ToArray();
            var closed = positions.Where(p => p.Status == PaperPositionStatus.Closed).ToArray();

            var realized = closed.Sum(p => p.RealizedPnl ?? 0m);
            var closedBasis = closed.Sum(p => p.CostBasis);

            return Results.Ok(new PortfolioResponse(
                open.Length,
                closed.Length,
                Math.Round(open.Sum(p => p.CostBasis), 2),
                Math.Round(realized, 2),
                closedBasis == 0m ? 0m : Math.Round(realized / closedBasis * 100m, 2),
                positions.Select(ResponseMapper.ToResponse).ToArray()));
        })
        .WithName("GetPaperPortfolio");

        return app;
    }

    private static async Task<decimal?> LastCloseAsync(RebelDbContext db, int instrumentId, CancellationToken ct)
    {
        var bar = await db.DailyBars
            .Where(b => b.InstrumentId == instrumentId)
            .OrderByDescending(b => b.Date)
            .FirstOrDefaultAsync(ct);

        return bar?.Close;
    }
}
