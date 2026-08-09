using Microsoft.EntityFrameworkCore;
using Rebel.Api.Contracts;
using Rebel.Core.Domain;
using Rebel.Infrastructure.Persistence;
using Rebel.Infrastructure.Services;

namespace Rebel.Api.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/sessions").RequireAuthorization().WithTags("Sessions");
        g.MapPost("/preflight", Preflight);
        g.MapPost("/start", Start);
        g.MapPost("/{id:guid}/end", End);
        g.MapGet("/{id:guid}", GetSession);
        g.MapGet("/history", History);
        g.MapPost("/{id:guid}/ask", Ask);
        return app;
    }

    private static string Uid(HttpContext ctx) =>
        ctx.User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    private static async Task<IResult> Preflight(PreflightRequest req, HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        if (!Guid.TryParse(req.PlaybookId, out var pid))
            return Results.BadRequest(new { message = "Invalid playbook ID." });

        var hasPlaybook = await db.Playbooks.AnyAsync(x => x.Id == pid && x.UserId == uid);
        var hasMarket = await db.ProviderConnections.AnyAsync(x => x.UserId == uid && x.ProviderType == ProviderType.MarketData && x.Status == ProviderStatus.Connected);
        var hasAI = await db.ProviderConnections.AnyAsync(x => x.UserId == uid && x.ProviderType == ProviderType.AI && x.Status == ProviderStatus.Connected);
        var hasBroker = await db.ProviderConnections.AnyAsync(x => x.UserId == uid && x.ProviderType == ProviderType.Broker && x.Status == ProviderStatus.Connected);

        return Results.Ok(new PreflightResponse(
            IsAuthenticated: true,
            HasActiveSubscription: true,
            HasLiveCoachingEntitlement: true,
            HasActivePlaybook: hasPlaybook,
            IsMarketDataConnected: hasMarket,
            IsAIConnected: hasAI,
            IsBrokerConnected: hasBroker));
    }

    private static async Task<IResult> Start(StartSessionRequest req, HttpContext ctx, RebelDbContext db, ICoachingSessionService svc)
    {
        var uid = Uid(ctx);
        var userName = ctx.User.FindFirst("username")?.Value ?? uid;

        if (!Guid.TryParse(req.PlaybookId, out var pid))
            return Results.BadRequest(new { message = "Invalid playbook ID." });

        var playbook = await db.Playbooks.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
        if (playbook == null) return Results.NotFound(new { message = "Playbook not found." });

        var session = await svc.StartAsync(uid, userName, playbook, req.Instrument);
        return Results.Ok(MapSession(session));
    }

    private static async Task<IResult> End(Guid id, HttpContext ctx, ICoachingSessionService svc)
    {
        var uid = Uid(ctx);
        var active = svc.GetActive(id);
        if (active == null || active.Session.UserId != uid) return Results.NotFound();
        await svc.EndAsync(id);
        return Results.Ok(MapSession(active.Session));
    }

    private static IResult GetSession(Guid id, HttpContext ctx, ICoachingSessionService svc)
    {
        var uid = Uid(ctx);
        var active = svc.GetActive(id);
        if (active == null || active.Session.UserId != uid) return Results.NotFound();
        return Results.Ok(MapSession(active.Session));
    }

    private static async Task<IResult> History(HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        var journals = await db.SessionJournals
            .Where(j => j.UserId == uid)
            .OrderByDescending(j => j.StartedAt)
            .Take(50)
            .ToListAsync();

        return Results.Ok(journals.Select(j => new SessionSummaryResponse(
            j.Id.ToString(), j.PlaybookName, j.Instrument,
            j.StartedAt.ToString("O"), j.EndedAt.ToString("O"),
            j.DurationMinutes, j.TradeCount, j.RealizedPnl, j.RuleViolations, j.ApiCost)));
    }

    private static async Task<IResult> Ask(Guid id, AskRequest req, HttpContext ctx, ICoachingSessionService svc)
    {
        var uid = Uid(ctx);
        var active = svc.GetActive(id);
        if (active == null || active.Session.UserId != uid) return Results.NotFound();
        var answer = await svc.AskAsync(id, uid, req.Question);
        return Results.Ok(new { messageId = Guid.NewGuid().ToString(), answer });
    }

    private static SessionResponse MapSession(TradingSession s) => new(
        s.Id.ToString(), s.PlaybookId.ToString(), s.PlaybookName, s.Instrument,
        s.AssetClass.ToString(), s.Status.ToString(),
        s.StartedAt.ToString("O"), s.EndedAt?.ToString("O"),
        s.SessionPnl, s.TradeCount);
}
