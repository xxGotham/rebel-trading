using Rebel.Core.Abstractions;
using Rebel.Core.Trading;

namespace Rebel.Infrastructure.Providers.MarketData;

public class SyntheticMarketDataProvider : IMarketDataProvider
{
    private static readonly Dictionary<string, decimal> BasePrices = new()
    {
        ["MNQ"] = 19_500m, ["NQ"] = 19_500m, ["ES"] = 5_400m,
        ["AAPL"] = 225m, ["TSLA"] = 250m, ["SPY"] = 540m,
    };

    private readonly Dictionary<string, decimal> _current = new();
    private readonly Random _rng = new();

    public string ProviderId => "synthetic";
    public string ProviderName => "Synthetic (Demo)";

    public Task<Quote?> GetQuoteAsync(string instrument, CancellationToken ct = default)
    {
        var price = Price(instrument);
        var base_ = BasePrices.GetValueOrDefault(instrument, 100m);
        var change = price - base_;
        var changePct = base_ == 0 ? 0 : change / base_;
        var spread = IsFutures(instrument) ? 0.25m : 0.01m;
        return Task.FromResult<Quote?>(
            new Quote(instrument, price, price - spread, price + spread, change, changePct, DateTimeOffset.UtcNow));
    }

    public async IAsyncEnumerable<MarketTick> StreamTicksAsync(
        string instrument,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(500, ct);
            var p = NextPrice(instrument);
            var spread = IsFutures(instrument) ? 0.25m : 0.01m;
            yield return new MarketTick(instrument, p, p - spread, p + spread, DateTimeOffset.UtcNow);
        }
    }

    private decimal Price(string instrument) =>
        _current.TryGetValue(instrument, out var p) ? p : BasePrices.GetValueOrDefault(instrument, 100m);

    private decimal NextPrice(string instrument)
    {
        var tick = IsFutures(instrument) ? 0.25m : 0.01m;
        var drift = (decimal)(_rng.NextDouble() * 3 * (_rng.NextDouble() < 0.52 ? 1 : -1));
        var next = Math.Max(Price(instrument) + drift * tick, tick);
        _current[instrument] = next;
        return next;
    }

    private static bool IsFutures(string s) => s is "MNQ" or "NQ" or "ES" or "MES" or "RTY";
}
