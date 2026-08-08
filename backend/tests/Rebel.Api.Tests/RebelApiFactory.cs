using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rebel.Core.Abstractions;
using Rebel.Infrastructure.Ai;
using Rebel.Infrastructure.Configuration;
using Rebel.Infrastructure.MarketData;
using Rebel.Infrastructure.Persistence;

namespace Rebel.Api.Tests;

/// <summary>
/// Hosts the real application over a throwaway SQLite file, with providers
/// pinned to the offline implementations.
/// <para>
/// Nothing here substitutes a mock for a pipeline stage: the tests exercise the
/// same ingestion, scoring, planning, grading and persistence code that runs in
/// production. Only the outermost vendor calls are replaced, which is what makes
/// these tests worth running.
/// </para>
/// <para>
/// The overrides are applied to the service collection rather than to
/// configuration, so the test host cannot silently fall back to the shipped
/// <c>appsettings.json</c> — which would have every test class sharing one
/// database file and produce results that depend on execution order.
/// </para>
/// </summary>
public sealed class RebelApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"rebel-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<RebelDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<RebelDbContext>();
            services.AddDbContext<RebelDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));

            services.RemoveAll<IMarketDataProvider>();
            services.RemoveAll<INewsProvider>();
            services.RemoveAll<IThesisGenerator>();

            services.AddScoped<IMarketDataProvider, SyntheticMarketDataProvider>();
            services.AddScoped<INewsProvider, NullNewsProvider>();
            services.AddScoped<IThesisGenerator, TemplateThesisGenerator>();

            services.Configure<PipelineOptions>(options =>
            {
                options.EnableScheduledRuns = false;
                // Shorter history keeps the suite fast while staying well clear of
                // the 60-bar minimum the signal layer enforces.
                options.HistoryDays = 200;
                options.MaxRecommendations = 3;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        // Release the SQLite handle before deleting the file.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    public RebelDbContext CreateDbContext() =>
        Services.CreateScope().ServiceProvider.GetRequiredService<RebelDbContext>();
}
