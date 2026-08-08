namespace Rebel.Infrastructure.Configuration;

public sealed class AlpacaOptions
{
    public const string Section = "Rebel:Alpaca";

    public string KeyId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string DataBaseUrl { get; set; } = "https://data.alpaca.markets";

    /// <summary>"iex" is included with a free account; "sip" needs a paid data subscription.</summary>
    public string Feed { get; set; } = "iex";

    /// <summary>Split-adjust history. Without this, every indicator breaks across a split.</summary>
    public string Adjustment { get; set; } = "split";

    public int MaxSymbolsPerRequest { get; set; } = 100;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(SecretKey);
}

public sealed class OpenAiOptions
{
    public const string Section = "Rebel:OpenAi";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-2024-08-06";
    public double Temperature { get; set; } = 0.4;
    public int MaxOutputTokens { get; set; } = 4000;
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Retry once with the violations quoted back when the model invents numbers.
    /// A second failure drops the recommendation rather than shipping it.
    /// </summary>
    public bool RetryOnNumberViolation { get; set; } = true;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed class PipelineOptions
{
    public const string Section = "Rebel:Pipeline";

    /// <summary>Sessions of history to pull. 260 ≈ one trading year, well past any indicator warm-up.</summary>
    public int HistoryDays { get; set; } = 400;

    /// <summary>How many scored names get news and go to the model.</summary>
    public int CandidateCount { get; set; } = 15;

    /// <summary>Maximum recommendations surfaced per run.</summary>
    public int MaxRecommendations { get; set; } = 3;

    public int NewsLookbackHours { get; set; } = 36;
    public int NewsPerSymbol { get; set; } = 5;

    /// <summary>Refuse to score if fewer than this many names have usable data — something upstream is broken.</summary>
    public int MinimumUniverseCoverage { get; set; } = 20;

    /// <summary>Cron for the pre-market run, in US/Eastern. Default 07:15 ET, before the 09:30 open.</summary>
    public string MorningRunCron { get; set; } = "15 7 * * 1-5";

    /// <summary>Cron for grading resolved recommendations, in US/Eastern. Default 17:30 ET, after the close.</summary>
    public string GradingCron { get; set; } = "30 17 * * 1-5";

    public bool EnableScheduledRuns { get; set; }
}

public sealed class ProviderOptions
{
    public const string Section = "Rebel:Providers";

    /// <summary>"alpaca" or "synthetic". Falls back to synthetic when Alpaca keys are absent.</summary>
    public string MarketData { get; set; } = "auto";

    /// <summary>"alpaca" or "none".</summary>
    public string News { get; set; } = "auto";

    /// <summary>"openai" or "template". Template writes deterministic prose with no LLM call.</summary>
    public string Thesis { get; set; } = "auto";
}
