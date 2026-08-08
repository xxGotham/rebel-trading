using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rebel.Core.Abstractions;
using Rebel.Core.Domain;
using Rebel.Infrastructure.Persistence;

namespace Rebel.Infrastructure.Pipeline;

public sealed record IngestionResult(int SymbolsRequested, int BarsInserted, int BarsUpdated, int SymbolsMissing)
{
    public override string ToString() =>
        $"{SymbolsRequested} symbols: +{BarsInserted} bars, ~{BarsUpdated} updated, {SymbolsMissing} with no data";
}

/// <summary>Pulls universe and price history into the database, idempotently.</summary>
public sealed class MarketDataIngestionService(
    RebelDbContext db,
    IUniverseProvider universe,
    IMarketDataProvider marketData,
    IClock clock,
    ILogger<MarketDataIngestionService> log)
{
    public async Task<int> SyncUniverseAsync(CancellationToken ct = default)
    {
        var entries = await universe.GetUniverseAsync(ct);
        var existing = await db.Instruments.ToDictionaryAsync(i => i.Symbol, StringComparer.OrdinalIgnoreCase, ct);
        var now = clock.UtcNow;
        var changed = 0;

        foreach (var entry in entries)
        {
            if (existing.TryGetValue(entry.Symbol, out var instrument))
            {
                if (instrument.Name == entry.Name && instrument.Sector == entry.Sector &&
                    instrument.SectorEtf == entry.SectorEtf && instrument.IsBenchmark == entry.IsBenchmark &&
                    instrument.IsActive)
                    continue;

                instrument.Name = entry.Name;
                instrument.Sector = entry.Sector;
                instrument.SectorEtf = entry.SectorEtf;
                instrument.Exchange = entry.Exchange;
                instrument.IsBenchmark = entry.IsBenchmark;
                instrument.IsActive = true;
                instrument.UpdatedAtUtc = now;
            }
            else
            {
                db.Instruments.Add(new Instrument
                {
                    Symbol = entry.Symbol,
                    Name = entry.Name,
                    Sector = entry.Sector,
                    SectorEtf = entry.SectorEtf,
                    Exchange = entry.Exchange,
                    IsBenchmark = entry.IsBenchmark,
                    IsActive = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            changed++;
        }

        if (changed > 0) await db.SaveChangesAsync(ct);
        log.LogInformation("Universe sync: {Changed} of {Total} instruments created or updated", changed, entries.Count);
        return changed;
    }

    public async Task<IngestionResult> IngestBarsAsync(
        IReadOnlyList<Instrument> instruments, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (instruments.Count == 0) return new IngestionResult(0, 0, 0, 0);

        var symbols = instruments.Select(i => i.Symbol).ToArray();
        var bySymbol = instruments.ToDictionary(i => i.Symbol, StringComparer.OrdinalIgnoreCase);
        var fetched = await marketData.GetDailyBarsAsync(symbols, from, to, ct);

        var ids = instruments.Select(i => i.Id).ToArray();
        var existing = await db.DailyBars
            .Where(b => ids.Contains(b.InstrumentId) && b.Date >= from && b.Date <= to)
            .ToDictionaryAsync(b => (b.InstrumentId, b.Date), ct);

        int inserted = 0, updated = 0, missing = 0;

        foreach (var symbol in symbols)
        {
            if (!fetched.TryGetValue(symbol, out var bars) || bars.Count == 0)
            {
                missing++;
                continue;
            }

            var instrumentId = bySymbol[symbol].Id;

            foreach (var bar in bars)
            {
                if (existing.TryGetValue((instrumentId, bar.Date), out var current))
                {
                    // Corporate actions rewrite history: a split makes every prior
                    // bar wrong, so re-ingested values overwrite rather than skip.
                    if (current.Open == bar.Open && current.High == bar.High &&
                        current.Low == bar.Low && current.Close == bar.Close &&
                        current.Volume == bar.Volume)
                        continue;

                    current.Open = bar.Open;
                    current.High = bar.High;
                    current.Low = bar.Low;
                    current.Close = bar.Close;
                    current.Volume = bar.Volume;
                    current.Vwap = bar.Vwap;
                    updated++;
                }
                else
                {
                    bar.InstrumentId = instrumentId;
                    db.DailyBars.Add(bar);
                    inserted++;
                }
            }
        }

        if (inserted > 0 || updated > 0) await db.SaveChangesAsync(ct);

        var result = new IngestionResult(symbols.Length, inserted, updated, missing);
        log.LogInformation("Bar ingestion from {Provider}: {Result}", marketData.Name, result);
        return result;
    }
}
