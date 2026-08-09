using Rebel.Core.Trading;

namespace Rebel.Core.Abstractions;

public interface IBrokerProvider
{
    string ProviderId { get; }
    string ProviderName { get; }
    Task<LivePosition?> GetPositionAsync(string instrument, CancellationToken ct = default);
    Task<IReadOnlyList<LiveOrder>> GetOpenOrdersAsync(string instrument, CancellationToken ct = default);
    Task<decimal> GetAccountBalanceAsync(CancellationToken ct = default);
}
