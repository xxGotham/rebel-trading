using Rebel.Core.Abstractions;
using Rebel.Core.Trading;

namespace Rebel.Infrastructure.Providers.Broker;

public class SyntheticBrokerProvider : IBrokerProvider
{
    public string ProviderId => "synthetic";
    public string ProviderName => "Synthetic (Demo)";

    public Task<LivePosition?> GetPositionAsync(string instrument, CancellationToken ct = default)
        => Task.FromResult<LivePosition?>(null);

    public Task<IReadOnlyList<LiveOrder>> GetOpenOrdersAsync(string instrument, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LiveOrder>>([]);

    public Task<decimal> GetAccountBalanceAsync(CancellationToken ct = default)
        => Task.FromResult(50_000m);
}
