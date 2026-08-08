using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebel.Core.Abstractions;
using Rebel.Core.Domain;
using Rebel.Infrastructure.Configuration;

namespace Rebel.Infrastructure.MarketData;

/// <summary>Alpaca's news feed (Benzinga-sourced), included with a data account.</summary>
public sealed class AlpacaNewsProvider : INewsProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AlpacaOptions _options;
    private readonly ILogger<AlpacaNewsProvider> _log;

    public AlpacaNewsProvider(
        HttpClient http,
        IOptions<AlpacaOptions> options,
        ILogger<AlpacaNewsProvider> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public string Name => "alpaca";

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<NewsItem>>> GetNewsAsync(
        IReadOnlyList<string> symbols,
        DateTime sinceUtc,
        int limitPerSymbol,
        CancellationToken ct = default)
    {
        var result = symbols.ToDictionary(
            s => s,
            _ => new List<NewsItem>(),
            StringComparer.OrdinalIgnoreCase);

        if (symbols.Count == 0) return Freeze(result, limitPerSymbol);

        // News is only fetched for the shortlist, so one batched request per
        // chunk is enough — no need to page deeply.
        foreach (var chunk in symbols.Chunk(Math.Min(_options.MaxSymbolsPerRequest, 50)))
        {
            var url = $"{_options.DataBaseUrl}/v1beta1/news" +
                      $"?symbols={string.Join(',', chunk)}" +
                      $"&start={sinceUtc:yyyy-MM-ddTHH:mm:ssZ}" +
                      $"&limit={Math.Min(50, limitPerSymbol * chunk.Length)}" +
                      $"&sort=desc";

            NewsResponse? payload;
            try
            {
                using var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    // News is enrichment, not a hard dependency: a failure here
                    // should degrade the thesis, not abort the run.
                    _log.LogWarning("Alpaca news request failed with {Status}; continuing without news",
                        (int)response.StatusCode);
                    continue;
                }

                payload = await response.Content.ReadFromJsonAsync<NewsResponse>(Json, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _log.LogWarning(ex, "Alpaca news request errored; continuing without news");
                continue;
            }

            if (payload?.News is null) continue;

            foreach (var article in payload.News)
            {
                foreach (var symbol in article.Symbols ?? [])
                {
                    if (!result.TryGetValue(symbol, out var list)) continue;

                    list.Add(new NewsItem
                    {
                        ExternalId = article.Id.ToString(),
                        Headline = article.Headline ?? string.Empty,
                        Summary = article.Summary ?? string.Empty,
                        Source = article.Source ?? "alpaca",
                        Url = article.Url ?? string.Empty,
                        PublishedAtUtc = article.CreatedAt.UtcDateTime,
                        FetchedAtUtc = DateTime.UtcNow
                    });
                }
            }
        }

        return Freeze(result, limitPerSymbol);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<NewsItem>> Freeze(
        Dictionary<string, List<NewsItem>> source, int limitPerSymbol) =>
        source.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<NewsItem>)kv.Value
                .DistinctBy(n => n.ExternalId)
                .OrderByDescending(n => n.PublishedAtUtc)
                .Take(limitPerSymbol)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);

    private sealed record NewsResponse
    {
        public List<Article>? News { get; init; }
    }

    private sealed record Article
    {
        public long Id { get; init; }
        public string? Headline { get; init; }
        public string? Summary { get; init; }
        public string? Source { get; init; }
        public string? Url { get; init; }
        public List<string>? Symbols { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }
    }
}
