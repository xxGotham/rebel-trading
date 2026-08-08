using Rebel.Core.Ai;
using Rebel.Core.Domain;

namespace Rebel.Core.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed record UniverseEntry(
    string Symbol,
    string Name,
    string Sector,
    string SectorEtf,
    string Exchange,
    bool IsBenchmark);

public interface IUniverseProvider
{
    Task<IReadOnlyList<UniverseEntry>> GetUniverseAsync(CancellationToken ct = default);
}

public sealed record BarRequest(string Symbol, DateOnly From, DateOnly To);

public interface IMarketDataProvider
{
    string Name { get; }

    Task<IReadOnlyList<DailyBar>> GetDailyBarsAsync(BarRequest request, CancellationToken ct = default);

    /// <summary>Daily bars for many symbols. Providers that batch should override the naive loop.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<DailyBar>>> GetDailyBarsAsync(
        IReadOnlyList<string> symbols, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Latest quote per symbol, including pre-market. Symbols with no data are omitted.</summary>
    Task<IReadOnlyDictionary<string, Snapshot>> GetSnapshotsAsync(
        IReadOnlyList<string> symbols, CancellationToken ct = default);
}

public interface INewsProvider
{
    string Name { get; }

    Task<IReadOnlyDictionary<string, IReadOnlyList<NewsItem>>> GetNewsAsync(
        IReadOnlyList<string> symbols,
        DateTime sinceUtc,
        int limitPerSymbol,
        CancellationToken ct = default);
}

public interface IThesisGenerator
{
    string Model { get; }
    string PromptVersion { get; }

    Task<ThesisResponse> GenerateAsync(ThesisRequest request, CancellationToken ct = default);
}
