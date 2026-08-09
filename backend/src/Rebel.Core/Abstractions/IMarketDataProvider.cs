using Rebel.Core.Trading;

namespace Rebel.Core.Abstractions;

public interface IMarketDataProvider
{
    string ProviderId { get; }
    string ProviderName { get; }
    Task<Quote?> GetQuoteAsync(string instrument, CancellationToken ct = default);
    IAsyncEnumerable<MarketTick> StreamTicksAsync(string instrument, CancellationToken ct = default);
}
