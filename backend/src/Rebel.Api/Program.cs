using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Rebel.Api.Endpoints;
using Rebel.Api.Hubs;
using Rebel.Infrastructure;
using Rebel.Infrastructure.Configuration;
using Rebel.Infrastructure.Persistence;
using Rebel.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway injects PORT; default to 8080 for local Docker runs
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://+:{port}");

// Railway injects DATABASE_URL (postgresql://user:pass@host:port/db)
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrEmpty(databaseUrl))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    builder.Configuration["ConnectionStrings:Default"] =
        $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')}" +
        $";Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo[1])}" +
        $";SSL Mode=Require;Trust Server Certificate=true";
}

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true).AllowCredentials()));

// JWT Bearer authentication — registered here because JwtBearer is a web SDK package
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSection["Secret"] ?? "rebel-trading-dev-secret-key-minimum-32-chars!";

builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSection["Issuer"] ?? "rebel-trading",
        ValidAudience = jwtSection["Audience"] ?? "rebel-trading-clients",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
    };
    // SignalR passes the token in the query string
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"].ToString();
            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                ctx.Token = token;
            return Task.CompletedTask;
        },
    };
});

// Singleton coaching service — active sessions must survive across requests
builder.Services.AddSingleton<ICoachingSessionService>(sp =>
{
    var hub = sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<CoachingHub>>();
    var market = sp.GetRequiredService<Rebel.Core.Abstractions.IMarketDataProvider>();
    var broker = sp.GetRequiredService<Rebel.Core.Abstractions.IBrokerProvider>();
    var ai = sp.GetRequiredService<Rebel.Core.Abstractions.IAIProvider>();
    var rules = sp.GetRequiredService<Rebel.Core.Trading.IRulesEngine>();
    var log = sp.GetRequiredService<ILogger<CoachingSessionService<CoachingHub>>>();
    return new CoachingSessionService<CoachingHub>(hub, market, broker, ai, rules, log);
});

var app = builder.Build();

// Initialize schema in the background so the health check can pass immediately.
// Retries with backoff in case the DB container isn't ready yet.
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RebelDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            await db.Database.EnsureCreatedAsync();
            log.LogInformation("Database ready.");
            return;
        }
        catch (Exception ex)
        {
            log.LogWarning("DB not ready (attempt {Attempt}/10): {Msg}", attempt, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(attempt * 3));
        }
    }
    log.LogError("Database failed to initialize after 10 attempts.");
});

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.MapAuthEndpoints();
app.MapPlaybookEndpoints();
app.MapProviderEndpoints();
app.MapSessionEndpoints();
app.MapHub<CoachingHub>("/hubs/coaching");

app.Run();

public partial class Program { }
