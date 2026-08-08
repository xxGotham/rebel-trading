using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rebel.Api.Endpoints;
using Rebel.Api.Jobs;
using Rebel.Core.Market;
using Rebel.Infrastructure;
using Rebel.Infrastructure.Configuration;
using Rebel.Infrastructure.Persistence;
using Rebel.Infrastructure.Pipeline;

var builder = WebApplication.CreateBuilder(args);

// Secrets come from environment variables or user-secrets, never appsettings.
// Rebel__Alpaca__KeyId, Rebel__OpenAi__ApiKey, ConnectionStrings__Rebel …
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddRebelInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<RebelJobs>();

var corsOrigins = builder.Configuration.GetSection("Rebel:Cors:Origins").Get<string[]>()
                  ?? ["http://localhost:5173", "http://localhost:4173"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Hangfire's in-memory storage is process-global, so several test hosts in one
// process contend on the same recurring-job locks. The scheduler is a trigger,
// not behaviour under test, so it stays out of the test host entirely.
var backgroundJobsEnabled = !builder.Environment.IsEnvironment("Testing");
if (backgroundJobsEnabled) AddBackgroundJobs(builder);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    if (backgroundJobsEnabled) app.UseHangfireDashboard("/hangfire");
}

app.MapSystemEndpoints();
app.MapRunEndpoints();
app.MapRecommendationEndpoints();
app.MapPerformanceEndpoints();
app.MapPaperTradingEndpoints();

await InitialiseDatabaseAsync(app);
if (backgroundJobsEnabled) ScheduleRecurringJobs(app);

app.Run();

static void AddBackgroundJobs(WebApplicationBuilder builder)
{
    var connectionString = builder.Configuration.GetConnectionString("Rebel");
    var usePostgres = !string.IsNullOrWhiteSpace(connectionString) &&
                      !connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);

    builder.Services.AddHangfire(config =>
    {
        config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
              .UseSimpleAssemblyNameTypeSerializer()
              .UseRecommendedSerializerSettings();

        // Memory storage keeps the no-Docker path working; jobs simply do not
        // survive a restart, which is acceptable for a schedule that re-fires daily.
        if (usePostgres) config.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString));
        else config.UseMemoryStorage();
    });

    builder.Services.AddHangfireServer(options => options.WorkerCount = 2);
}

static async Task InitialiseDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RebelDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (db.IsPostgres)
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Postgres schema migrated");
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
        logger.LogInformation("SQLite schema ready at {Connection}", db.Database.GetConnectionString());
    }

    var ingestion = scope.ServiceProvider.GetRequiredService<MarketDataIngestionService>();
    await ingestion.SyncUniverseAsync();
}

static void ScheduleRecurringJobs(WebApplication app)
{
    var options = app.Services.GetRequiredService<IOptions<PipelineOptions>>().Value;
    var recurring = app.Services.GetRequiredService<IRecurringJobManager>();

    if (!options.EnableScheduledRuns)
    {
        // Removing rather than skipping: flipping the flag off must actually stop
        // the schedule, including one persisted by a previous run.
        recurring.RemoveIfExists(RebelJobs.MorningRunId);
        recurring.RemoveIfExists(RebelJobs.GradingId);
        return;
    }

    var jobOptions = new RecurringJobOptions { TimeZone = MarketCalendar.Eastern };

    recurring.AddOrUpdate<RebelJobs>(
        RebelJobs.MorningRunId,
        job => job.MorningRunAsync(CancellationToken.None),
        options.MorningRunCron,
        jobOptions);

    recurring.AddOrUpdate<RebelJobs>(
        RebelJobs.GradingId,
        job => job.GradeAsync(CancellationToken.None),
        options.GradingCron,
        jobOptions);
}

/// <summary>Exposed so the integration tests can host the real application.</summary>
public partial class Program;
