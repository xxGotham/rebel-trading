using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebel.Core.Abstractions;
using Rebel.Core.Scoring;
using Rebel.Core.Trading;
using Rebel.Infrastructure.Ai;
using Rebel.Infrastructure.Configuration;
using Rebel.Infrastructure.MarketData;
using Rebel.Infrastructure.Persistence;
using Rebel.Infrastructure.Pipeline;
using Rebel.Infrastructure.Universe;

namespace Rebel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRebelInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AlpacaOptions>(configuration.GetSection(AlpacaOptions.Section));
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.Section));
        services.Configure<PipelineOptions>(configuration.GetSection(PipelineOptions.Section));
        services.Configure<ProviderOptions>(configuration.GetSection(ProviderOptions.Section));
        services.Configure<ScoringWeights>(configuration.GetSection("Rebel:Scoring"));
        services.Configure<RiskSettings>(configuration.GetSection("Rebel:Risk"));

        AddPersistence(services, configuration);
        AddHttpClients(services, configuration);
        AddProviders(services, configuration);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IUniverseProvider>(_ => new CsvUniverseProvider());

        services.AddSingleton(sp => new ScoringEngine(sp.GetRequiredService<IOptions<ScoringWeights>>().Value));
        services.AddSingleton(sp => new TradePlanBuilder(sp.GetRequiredService<IOptions<RiskSettings>>().Value));

        services.AddScoped<MarketDataIngestionService>();
        services.AddScoped<AnalysisPipeline>();
        services.AddScoped<GradingService>();

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Rebel");

        services.AddDbContext<RebelDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString) ||
                connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                // No Postgres configured: fall back to a local SQLite file so the
                // app is runnable without Docker. Schema is provider-agnostic.
                options.UseSqlite(connectionString ?? "Data Source=rebel.db");
            }
            else
            {
                options.UseNpgsql(connectionString);
            }
        });
    }

    private static void AddHttpClients(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(AlpacaMarketDataProvider.HttpClientName, (sp, client) =>
        {
            var alpaca = sp.GetRequiredService<IOptions<AlpacaOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("APCA-API-KEY-ID", alpaca.KeyId);
            client.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", alpaca.SecretKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddHttpClient(OpenAiThesisGenerator.HttpClientName, (sp, client) =>
        {
            var openAi = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(openAi.TimeoutSeconds);
            if (openAi.IsConfigured)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAi.ApiKey);
        });
    }

    /// <summary>
    /// Provider selection. "auto" picks the real vendor when credentials exist and
    /// the offline stand-in when they don't, so a fresh clone runs end to end
    /// before anyone has signed up for anything.
    /// </summary>
    private static void AddProviders(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SyntheticMarketDataProvider>();
        services.AddSingleton<NullNewsProvider>();
        services.AddSingleton<TemplateThesisGenerator>();

        services.AddScoped<IMarketDataProvider>(sp =>
        {
            var providers = sp.GetRequiredService<IOptions<ProviderOptions>>().Value;
            var alpaca = sp.GetRequiredService<IOptions<AlpacaOptions>>().Value;
            var log = sp.GetRequiredService<ILogger<SyntheticMarketDataProvider>>();

            if (Uses(providers.MarketData, "alpaca", alpaca.IsConfigured))
                return new AlpacaMarketDataProvider(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient(AlpacaMarketDataProvider.HttpClientName),
                    sp.GetRequiredService<IOptions<AlpacaOptions>>(),
                    sp.GetRequiredService<ILogger<AlpacaMarketDataProvider>>());

            log.LogWarning(
                "Using SYNTHETIC market data — prices are generated, not real. Set Rebel:Alpaca credentials for live data.");
            return sp.GetRequiredService<SyntheticMarketDataProvider>();
        });

        services.AddScoped<INewsProvider>(sp =>
        {
            var providers = sp.GetRequiredService<IOptions<ProviderOptions>>().Value;
            var alpaca = sp.GetRequiredService<IOptions<AlpacaOptions>>().Value;

            if (Uses(providers.News, "alpaca", alpaca.IsConfigured))
                return new AlpacaNewsProvider(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient(AlpacaMarketDataProvider.HttpClientName),
                    sp.GetRequiredService<IOptions<AlpacaOptions>>(),
                    sp.GetRequiredService<ILogger<AlpacaNewsProvider>>());

            return sp.GetRequiredService<NullNewsProvider>();
        });

        services.AddScoped<IThesisGenerator>(sp =>
        {
            var providers = sp.GetRequiredService<IOptions<ProviderOptions>>().Value;
            var openAi = sp.GetRequiredService<IOptions<OpenAiOptions>>().Value;

            if (Uses(providers.Thesis, "openai", openAi.IsConfigured))
                return new OpenAiThesisGenerator(
                    sp.GetRequiredService<IHttpClientFactory>().CreateClient(OpenAiThesisGenerator.HttpClientName),
                    sp.GetRequiredService<IOptions<OpenAiOptions>>(),
                    sp.GetRequiredService<ILogger<OpenAiThesisGenerator>>());

            sp.GetRequiredService<ILogger<TemplateThesisGenerator>>().LogWarning(
                "Using the TEMPLATE thesis generator — no LLM call. Set Rebel:OpenAi:ApiKey to enable Rebel AI.");
            return sp.GetRequiredService<TemplateThesisGenerator>();
        });
    }

    private static bool Uses(string configured, string vendor, bool credentialsPresent) =>
        string.Equals(configured, vendor, StringComparison.OrdinalIgnoreCase) ||
        (string.Equals(configured, "auto", StringComparison.OrdinalIgnoreCase) && credentialsPresent);
}
