using Rebel.Core.Abstractions;
using Rebel.Core.Domain;
using Rebel.Core.Market;

namespace Rebel.Infrastructure.MarketData;

/// <summary>
/// Deterministic fake market data, used when no Alpaca credentials are present.
/// <para>
/// This exists so the pipeline is runnable and demonstrable on a laptop with no
/// vendor account, and so integration tests never touch the network. It is
/// <b>not</b> a simulator: the price paths are a seeded random walk with no
/// relationship to any real security. Every run stamped with this provider is
/// marked as such, and its recommendations must never be mixed into performance
/// analytics as if they were real.
/// </para>
/// </summary>
public sealed class SyntheticMarketDataProvider : IMarketDataProvider
{
    public const string ProviderName = "synthetic";

    /// <summary>Fixed anchor so a symbol's history is identical on every call and every process.</summary>
    private static readonly DateOnly Anchor = new(2015, 1, 2);

    public string Name => ProviderName;

    public Task<IReadOnlyList<DailyBar>> GetDailyBarsAsync(
        BarRequest request, CancellationToken ct = default) =>
        Task.FromResult(Generate(request.Symbol, request.From, request.To));

    public Task<IReadOnlyDictionary<string, IReadOnlyList<DailyBar>>> GetDailyBarsAsync(
        IReadOnlyList<string> symbols, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var result = symbols.ToDictionary(
            s => s,
            s => Generate(s, from, to),
            StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<DailyBar>>>(result);
    }

    public Task<IReadOnlyDictionary<string, Snapshot>> GetSnapshotsAsync(
        IReadOnlyList<string> symbols, CancellationToken ct = default)
    {
        var today = MarketCalendar.CurrentTradingDate(DateTime.UtcNow);
        var history = today.AddDays(-40);

        var result = new Dictionary<string, Snapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbol in symbols)
        {
            var bars = Generate(symbol, history, MarketCalendar.PreviousTradingDay(today));
            if (bars.Count == 0) continue;

            var last = bars[^1];
            var rng = new Random(Seed(symbol) ^ today.DayNumber);

            // Gap and pre-market participation are drawn with enough spread that
            // the cross-sectional z-scores in scoring have something to separate.
            var gap = (rng.NextDouble() - 0.42) * 0.05;
            var volumeRatio = 0.01 + rng.NextDouble() * 0.22;
            var avgVolume = (long)bars.TakeLast(20).Average(b => b.Volume);

            result[symbol] = new Snapshot
            {
                AsOfUtc = DateTime.UtcNow,
                LastPrice = Math.Round(last.Close * (decimal)(1 + gap), 2),
                PreviousClose = last.Close,
                SessionVolume = (long)(avgVolume * volumeRatio),
                DayHigh = Math.Round(last.Close * (decimal)(1 + Math.Max(gap, 0) + 0.002), 2),
                DayLow = Math.Round(last.Close * (decimal)(1 + Math.Min(gap, 0) - 0.002), 2)
            };
        }

        return Task.FromResult<IReadOnlyDictionary<string, Snapshot>>(result);
    }

    private static IReadOnlyList<DailyBar> Generate(string symbol, DateOnly from, DateOnly to)
    {
        if (to < from) return [];

        var seed = Seed(symbol);
        var rng = new Random(seed);

        var anchorPrice = 20d + seed % 580;
        var price = anchorPrice;
        var drift = -0.0004 + (seed % 97) / 97d * 0.0016;
        var vol = 0.010 + (seed % 53) / 53d * 0.025;
        var baseVolume = 1_500_000L + seed % 38_000_000L;

        // A pure random walk compounds over a decade into prices in the tens of
        // thousands or under a dollar, which then trips the real liquidity and
        // price filters and makes the demo look broken. A weak pull toward the
        // anchor keeps the series in a plausible range while still trending
        // locally, which is all the scoring layer needs.
        const double reversion = 0.004;

        var bars = new List<DailyBar>();

        // Walk forward from the anchor so the path for a given date never depends
        // on the requested window — the same date always produces the same bar.
        for (var date = Anchor; date <= to; date = date.AddDays(1))
        {
            if (!MarketCalendar.IsTradingDay(date)) continue;

            var pull = reversion * Math.Log(anchorPrice / price);
            var shock = NextGaussian(rng) * vol + drift + pull;
            var open = price;
            var close = Math.Max(1d, open * (1 + shock));
            var range = Math.Abs(close - open) + open * vol * rng.NextDouble();
            var high = Math.Max(open, close) + range * 0.4;
            var low = Math.Max(0.5, Math.Min(open, close) - range * 0.4);
            var volume = (long)(baseVolume * (0.6 + rng.NextDouble() * 0.9));

            price = close;

            if (date < from) continue;

            bars.Add(new DailyBar
            {
                Date = date,
                Open = Math.Round((decimal)open, 2),
                High = Math.Round((decimal)high, 2),
                Low = Math.Round((decimal)low, 2),
                Close = Math.Round((decimal)close, 2),
                Volume = volume,
                Vwap = Math.Round((decimal)((high + low + close) / 3), 2)
            });
        }

        return bars;
    }

    private static double NextGaussian(Random rng)
    {
        // Box–Muller; u1 is nudged off zero to keep the log finite.
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    private static int Seed(string symbol)
    {
        // Deliberately not string.GetHashCode(): that is randomised per process,
        // which would make "deterministic" data change on every restart.
        var hash = 17;
        foreach (var c in symbol.ToUpperInvariant()) hash = hash * 31 + c;
        return Math.Abs(hash);
    }
}

/// <summary>News provider used when no news vendor is configured.</summary>
public sealed class NullNewsProvider : INewsProvider
{
    public string Name => "none";

    public Task<IReadOnlyDictionary<string, IReadOnlyList<NewsItem>>> GetNewsAsync(
        IReadOnlyList<string> symbols,
        DateTime sinceUtc,
        int limitPerSymbol,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<NewsItem>>>(
            symbols.ToDictionary(
                s => s,
                _ => (IReadOnlyList<NewsItem>)[],
                StringComparer.OrdinalIgnoreCase));
}
