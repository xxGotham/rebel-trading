using Microsoft.EntityFrameworkCore;
using Rebel.Api.Contracts;
using Rebel.Core.Domain;
using Rebel.Infrastructure.Persistence;

namespace Rebel.Api.Endpoints;

public static class PlaybookEndpoints
{
    public static IEndpointRouteBuilder MapPlaybookEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/playbooks").RequireAuthorization().WithTags("Playbooks");
        g.MapGet("/", List);
        g.MapGet("/{id:guid}", Get);
        g.MapPost("/", Create);
        g.MapPut("/{id:guid}", Update);
        g.MapDelete("/{id:guid}", Delete);
        g.MapPost("/{id:guid}/activate", Activate);
        return app;
    }

    private static string Uid(HttpContext ctx) =>
        ctx.User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    private static async Task<IResult> List(HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        var list = await db.Playbooks.Where(p => p.UserId == uid).OrderByDescending(p => p.UpdatedAt).ToListAsync();
        return Results.Ok(list.Select(Map));
    }

    private static async Task<IResult> Get(Guid id, HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        var p = await db.Playbooks.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        return p == null ? Results.NotFound() : Results.Ok(Map(p));
    }

    private static async Task<IResult> Create(CreatePlaybookRequest req, HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        if (!Enum.TryParse<AssetClass>(req.AssetClass, true, out var ac))
            return Results.BadRequest(new { message = "Invalid asset class." });

        var p = new Playbook
        {
            UserId = uid,
            Name = req.Name,
            Description = req.Description,
            AssetClass = ac,
            Instruments = req.Instruments,
            IsActive = req.IsActive,
            Rules = MapRules(req.Rules),
        };
        db.Playbooks.Add(p);
        await db.SaveChangesAsync();
        return Results.Created($"/playbooks/{p.Id}", Map(p));
    }

    private static async Task<IResult> Update(Guid id, UpdatePlaybookRequest req, HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        var p = await db.Playbooks.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (p == null) return Results.NotFound();

        if (req.Name != null) p.Name = req.Name;
        if (req.Description != null) p.Description = req.Description;
        if (req.Instruments != null) p.Instruments = req.Instruments;
        if (req.Rules != null) p.Rules = MapRules(req.Rules);
        if (req.AssetClass != null && Enum.TryParse<AssetClass>(req.AssetClass, true, out var ac)) p.AssetClass = ac;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        p.Version++;

        await db.SaveChangesAsync();
        return Results.Ok(Map(p));
    }

    private static async Task<IResult> Delete(Guid id, HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        var p = await db.Playbooks.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
        if (p == null) return Results.NotFound();
        db.Playbooks.Remove(p);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> Activate(Guid id, HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        var all = await db.Playbooks.Where(x => x.UserId == uid).ToListAsync();
        foreach (var pb in all) pb.IsActive = pb.Id == id;
        await db.SaveChangesAsync();
        var target = all.FirstOrDefault(x => x.Id == id);
        return target == null ? Results.NotFound() : Results.Ok(Map(target));
    }

    private static PlaybookResponse Map(Playbook p) => new(
        p.Id.ToString(), p.Name, p.Description, p.AssetClass.ToString(),
        p.Instruments, p.IsActive, p.Version,
        p.CreatedAt.ToString("O"), p.UpdatedAt.ToString("O"),
        new PlaybookRulesResponse(
            p.Rules.TradingHoursStart, p.Rules.TradingHoursEnd,
            p.Rules.EntryConditions, p.Rules.ExitConditions,
            p.Rules.RiskPerTrade, p.Rules.MaxDailyRisk,
            p.Rules.StopLossRules, p.Rules.StopManagementRules,
            p.Rules.ProfitTargets, p.Rules.TrailingRules,
            p.Rules.PositionSizingRules, p.Rules.MaxTradesPerDay,
            p.Rules.AdditionalConditions));

    private static PlaybookRules MapRules(PlaybookRulesDto r) => new()
    {
        TradingHoursStart = r.TradingHoursStart,
        TradingHoursEnd = r.TradingHoursEnd,
        EntryConditions = r.EntryConditions,
        ExitConditions = r.ExitConditions,
        RiskPerTrade = r.RiskPerTrade,
        MaxDailyRisk = r.MaxDailyRisk,
        StopLossRules = r.StopLossRules,
        StopManagementRules = r.StopManagementRules,
        ProfitTargets = r.ProfitTargets,
        TrailingRules = r.TrailingRules,
        PositionSizingRules = r.PositionSizingRules,
        MaxTradesPerDay = r.MaxTradesPerDay,
        AdditionalConditions = r.AdditionalConditions,
    };
}
