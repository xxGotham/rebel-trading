using Microsoft.EntityFrameworkCore;
using Rebel.Api.Contracts;
using Rebel.Core.Domain;
using Rebel.Infrastructure.Persistence;

namespace Rebel.Api.Endpoints;

public static class ProviderEndpoints
{
    public static IEndpointRouteBuilder MapProviderEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/providers").RequireAuthorization().WithTags("Providers");
        g.MapGet("/", List);
        g.MapPost("/connect", Connect);
        g.MapDelete("/{providerId}", Disconnect);
        return app;
    }

    private static string Uid(HttpContext ctx) =>
        ctx.User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    private static async Task<IResult> List(HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        var list = await db.ProviderConnections.Where(x => x.UserId == uid).ToListAsync();
        return Results.Ok(list.Select(Map));
    }

    private static async Task<IResult> Connect(ConnectProviderRequest req, HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        if (!Enum.TryParse<ProviderType>(req.ProviderType, true, out var pt))
            return Results.BadRequest(new { message = "Invalid provider type." });
        if (!Enum.TryParse<KeySource>(req.KeySource, true, out var ks))
            return Results.BadRequest(new { message = "Invalid key source." });

        var existing = await db.ProviderConnections
            .FirstOrDefaultAsync(x => x.UserId == uid && x.ProviderId == req.ProviderId);

        if (existing != null)
        {
            existing.Status = ProviderStatus.Connected;
        }
        else
        {
            db.ProviderConnections.Add(new ProviderConnection
            {
                UserId = uid,
                ProviderId = req.ProviderId,
                ProviderName = req.ProviderName,
                ProviderType = pt,
                KeySource = ks,
                Status = ProviderStatus.Connected,
            });
        }

        await db.SaveChangesAsync();
        var result = await db.ProviderConnections.FirstAsync(x => x.UserId == uid && x.ProviderId == req.ProviderId);
        return Results.Ok(Map(result));
    }

    private static async Task<IResult> Disconnect(string providerId, HttpContext ctx, RebelDbContext db)
    {
        var uid = Uid(ctx);
        var conn = await db.ProviderConnections.FirstOrDefaultAsync(x => x.UserId == uid && x.ProviderId == providerId);
        if (conn == null) return Results.NotFound();
        db.ProviderConnections.Remove(conn);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static ProviderConnectionResponse Map(ProviderConnection c) =>
        new(c.ProviderId, c.ProviderName, c.ProviderType.ToString(), c.Status.ToString(), c.KeySource.ToString());
}
