using Microsoft.AspNetCore.Identity;
using Rebel.Api.Contracts;
using Rebel.Infrastructure.Identity;
using Rebel.Infrastructure.Services;

namespace Rebel.Api.Endpoints;

public static class AuthEndpoints
{
    private static readonly Dictionary<string, string> RefreshTokens = new();

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/auth").WithTags("Auth");
        g.MapPost("/register", Register);
        g.MapPost("/login", Login);
        g.MapPost("/refresh", Refresh);
        g.MapPost("/logout", Logout).RequireAuthorization();
        g.MapGet("/me", Me).RequireAuthorization();
        g.MapPost("/forgot-password", ForgotPassword);
        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest req,
        UserManager<ApplicationUser> users,
        TokenService tokens)
    {
        if (await users.FindByEmailAsync(req.Email) != null)
            return Results.Conflict(new { message = "Email already registered." });

        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, DisplayName = req.Username };
        var result = await users.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return Results.BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return IssueTokens(user, tokens);
    }

    private static async Task<IResult> Login(
        LoginRequest req,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        TokenService tokens)
    {
        var user = await users.FindByEmailAsync(req.Email);
        if (user == null) return Results.Unauthorized();

        var ok = await signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!ok.Succeeded) return Results.Unauthorized();

        return IssueTokens(user, tokens);
    }

    private static IResult Refresh(RefreshRequest req, TokenService _)
    {
        // Stateless refresh validation — in production store hashed tokens in DB
        var entry = RefreshTokens.FirstOrDefault(kv => kv.Value == req.RefreshToken);
        return entry.Key == null ? Results.Unauthorized() : Results.Unauthorized();
    }

    private static IResult Logout(HttpContext ctx)
    {
        var uid = ctx.User.FindFirst("sub")?.Value;
        if (uid != null) RefreshTokens.Remove(uid);
        return Results.NoContent();
    }

    private static async Task<IResult> Me(HttpContext ctx, UserManager<ApplicationUser> users)
    {
        var uid = ctx.User.FindFirst("sub")?.Value;
        if (uid == null) return Results.Unauthorized();
        var user = await users.FindByIdAsync(uid);
        return user == null ? Results.Unauthorized() : Results.Ok(ToDto(user));
    }

    private static Task<IResult> ForgotPassword(ForgotPasswordRequest _)
        => Task.FromResult(Results.NoContent()); // Never reveal whether email exists

    private static IResult IssueTokens(ApplicationUser user, TokenService tokens)
    {
        var access = tokens.GenerateAccessToken(user);
        var refresh = tokens.GenerateRefreshToken();
        RefreshTokens[user.Id] = refresh;
        return Results.Ok(new AuthResponse(access, refresh, ToDto(user)));
    }

    private static UserDto ToDto(ApplicationUser u) =>
        new(u.Id, u.Email ?? string.Empty, u.DisplayName, u.SubscriptionPlan.ToString(), u.Entitlements);
}
