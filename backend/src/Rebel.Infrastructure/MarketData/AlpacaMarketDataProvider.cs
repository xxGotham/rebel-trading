using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rebel.Core.Abstractions;
using Rebel.Core.Domain;
using Rebel.Core.Market;
using Rebel.Infrastructure.Configuration;

namespace Rebel.Infrastructure.MarketData;

/// <summary>
/// Alpaca Market Data v2. Chosen for V1 because one credential covers bars,
/// snapshots and news, and it is the same vendor the paper-trading phase will
/// need — so nothing here gets thrown away later.
/// </summary>
public sealed class AlpacaMarketDataProvider : IMarketDataProvider
{
    public const string HttpClientName = "alpaca-data";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AlpacaOptions _options;
    private readonly ILogger<AlpacaMarketDataProvider> _log;

    public AlpacaMarketDataProvider(
        HttpClient http,
        IOptions<AlpacaOptions> options,
        ILogger<AlpacaMarketDataProvider> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public string Name => "alpaca";

    public async Task<IReadOnlyList<DailyBar>> GetDailyBarsAsync(
        BarRequest request, CancellationToken ct = default)
    {
        var all = await GetDailyBarsAsync([request.Symbol], request.From, request.To, ct);
        return all.TryGetValue(request.Symbol, out var bars) ? bars : [];
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<DailyBar>>> GetDailyBarsAsync(
        IReadOnlyList<string> symbols, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var result = new Dictionary<string, List<DailyBar>>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in symbols.Chunk(_options.MaxSymbolsPerRequest))
        {
            string? pageToken = null;

            do
            {
                var url = BuildBarsUrl(chunk, from, to, pageToken);
                var payload = await GetAsync<BarsResponse>(url, ct);
                if (payload?.Bars is null) break;

                foreach (var (symbol, bars) in payload.Bars)
                {
                    if (!result.TryGetValue(symbol, out var list))
                        result[symbol] = list = [];

                    foreach (var bar in bars) list.Add(ToDailyBar(bar));
                }

                pageToken = payload.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken) && !ct.IsCancellationRequested);
        }

        return result.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<DailyBar>)kv.Value
                .GroupBy(b => b.Date)
                .Select(g => g.Last())
                .OrderBy(b => b.Date)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, Snapshot>> GetSnapshotsAsync(
        IReadOnlyList<string> symbols, CancellationToken ct = default)
    {
        var result = new Dictionary<string, Snapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in symbols.Chunk(_options.MaxSymbolsPerRequest))
        {
            var url = $"{_options.DataBaseUrl}/v2/stocks/snapshots" +
                      $"?symbols={string.Join(',', chunk)}&feed={_options.Feed}";

            var document = await GetAsync<JsonElement>(url, ct);
            if (document.ValueKind != JsonValueKind.Object) continue;

            // Alpaca has shipped both a bare symbol map and a {"snapshots": {...}}
            // envelope for this endpoint; accept either rather than break on a
            // vendor-side shape change.
            var map = document.TryGetProperty("snapshots", out var wrapped) ? wrapped : document;

            foreach (var property in map.EnumerateObject())
            {
                var snapshot = ToSnapshot(property.Value);
                if (snapshot is not null) result[property.Name] = snapshot;
            }
        }

        _log.LogDebug("Alpaca returned {Count} snapshots for {Requested} symbols", result.Count, symbols.Count);
        return result;
    }

    private string BuildBarsUrl(IEnumerable<string> chunk, DateOnly from, DateOnly to, string? pageToken)
    {
        var url = $"{_options.DataBaseUrl}/v2/stocks/bars" +
                  $"?symbols={string.Join(',', chunk)}" +
                  $"&timeframe=1Day" +
                  $"&start={from:yyyy-MM-dd}" +
                  $"&end={to:yyyy-MM-dd}" +
                  $"&adjustment={_options.Adjustment}" +
                  $"&feed={_options.Feed}" +
                  $"&limit=10000";

        return pageToken is null ? url : $"{url}&page_token={Uri.EscapeDataString(pageToken)}";
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Alpaca request failed ({(int)response.StatusCode}): {Truncate(body, 400)}");
        }

        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private static DailyBar ToDailyBar(AlpacaBar bar) => new()
    {
        // Alpaca stamps daily bars at the session open in UTC; converting to
        // Eastern is what yields the correct session date on both sides of DST.
        Date = DateOnly.FromDateTime(MarketCalendar.ToEastern(bar.T.UtcDateTime)),
        Open = bar.O,
        High = bar.H,
        Low = bar.L,
        Close = bar.C,
        Volume = bar.V,
        Vwap = bar.Vw
    };

    private static Snapshot? ToSnapshot(JsonElement element)
    {
        var lastPrice = Decimal(element, "latestTrade", "p")
                        ?? Decimal(element, "minuteBar", "c")
                        ?? Decimal(element, "dailyBar", "c");

        var previousClose = Decimal(element, "prevDailyBar", "c");
        if (lastPrice is null || previousClose is null) return null;

        var asOf = Timestamp(element, "latestTrade", "t")
                   ?? Timestamp(element, "minuteBar", "t")
                   ?? DateTime.UtcNow;

        return new Snapshot
        {
            AsOfUtc = asOf,
            LastPrice = lastPrice.Value,
            PreviousClose = previousClose.Value,
            SessionVolume = (long?)Decimal(element, "dailyBar", "v") ?? 0L,
            DayHigh = Decimal(element, "dailyBar", "h"),
            DayLow = Decimal(element, "dailyBar", "l")
        };
    }

    private static decimal? Decimal(JsonElement root, string objectName, string field) =>
        root.TryGetProperty(objectName, out var obj) &&
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(field, out var value) &&
        value.TryGetDecimal(out var parsed)
            ? parsed
            : null;

    private static DateTime? Timestamp(JsonElement root, string objectName, string field) =>
        root.TryGetProperty(objectName, out var obj) &&
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(field, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length] + "…";

    private sealed record BarsResponse
    {
        public Dictionary<string, List<AlpacaBar>>? Bars { get; init; }

        [JsonPropertyName("next_page_token")]
        public string? NextPageToken { get; init; }
    }

    private sealed record AlpacaBar
    {
        public DateTimeOffset T { get; init; }
        public decimal O { get; init; }
        public decimal H { get; init; }
        public decimal L { get; init; }
        public decimal C { get; init; }
        public long V { get; init; }
        public decimal? Vw { get; init; }
    }
}
