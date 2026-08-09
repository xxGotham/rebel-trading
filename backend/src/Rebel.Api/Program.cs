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

// Create schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RebelDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapPlaybookEndpoints();
app.MapProviderEndpoints();
app.MapSessionEndpoints();
app.MapHub<CoachingHub>("/hubs/coaching");

app.Run();

public partial class Program { }
