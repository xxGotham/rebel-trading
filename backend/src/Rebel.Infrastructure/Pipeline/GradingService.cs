using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rebel.Core.Abstractions;
using Rebel.Core.Domain;
using Rebel.Core.Market;
using Rebel.Core.Performance;
using Rebel.Infrastructure.Persistence;

namespace Rebel.Infrastructure.Pipeline;

public sealed record GradingSummary(int Examined, int Graded, int StillOpen)
{
    public override string ToString() => $"{Examined} examined, {Graded} graded, {StillOpen} still open";
}

/// <summary>
/// Grades recommendations against what the market actually did.
/// <para>
/// This runs unconditionally over every recommendation ever made — including the
/// ones that look bad. That is the point of the whole accountability layer: a
/// track record is only worth something if nothing can be quietly excluded from
/// it.
/// </para>
/// </summary>
public sealed class GradingService(
    RebelDbContext db,
    MarketDataIngestionService ingestion,
    IClock clock,
    ILogger<GradingService> log)
{
    public async Task<GradingSummary> GradeOutstandingAsync(CancellationToken ct = default)
    {
        var pending = await db.Recommendations
            .Include(r => r.Outcome)
            .Where(r => r.Outcome == null || r.Outcome.Status == OutcomeStatus.Open)
            .ToListAsync(ct);

        if (pending.Count == 0) return new GradingSummary(0, 0, 0);

        var instrumentIds = pending.Select(r => r.InstrumentId).Distinct().ToArray();
        var earliest = pending.Min(r => r.TradingDate);
        var today = MarketCalendar.CurrentTradingDate(clock.UtcNow);

        // Grading needs sessions after the recommendation, which routine ingestion
        // may not have pulled yet for names that dropped out of the universe.
        var instruments = await db.Instruments
            .Where(i => instrumentIds.Contains(i.Id) || i.Symbol == "SPY")
            .ToListAsync(ct);

        await ingestion.IngestBarsAsync(instruments, earliest, today, ct);

        var bars = await db.DailyBars
            .Where(b => b.Date >= earliest)
            .OrderBy(b => b.Date)
            .ToListAsync(ct);

        var barsByInstrument = bars.GroupBy(b => b.InstrumentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DailyBar>)g.ToList());

        var spy = instruments.FirstOrDefault(i => i.Symbol == "SPY");
        var benchmarkBars = spy is not null
            ? barsByInstrument.GetValueOrDefault(spy.Id) ?? []
            : [];

        int graded = 0, open = 0;

        foreach (var rec in pending)
        {
            var forward = barsByInstrument.GetValueOrDefault(rec.InstrumentId) ?? [];

            var result = Grader.Grade(new GradeInput
            {
                TradingDate = rec.TradingDate,
                EntryLow = rec.EntryLow,
                EntryHigh = rec.EntryHigh,
                Target = rec.Target,
                StopLoss = rec.StopLoss,
                HoldingPeriodDays = rec.HoldingPeriodDays,
                ForwardBars = forward.Where(b => b.Date >= rec.TradingDate).ToArray(),
                BenchmarkBars = benchmarkBars
            });

            if (result is null)
            {
                open++;
                Upsert(rec, new GradeResult { Status = OutcomeStatus.Open }, graded: false);
                continue;
            }

            Upsert(rec, result, graded: true);
            graded++;
        }

        await db.SaveChangesAsync(ct);

        var summary = new GradingSummary(pending.Count, graded, open);
        log.LogInformation("Grading complete: {Summary}", summary);
        return summary;
    }

    private void Upsert(Recommendation rec, GradeResult result, bool graded)
    {
        var outcome = rec.Outcome;

        if (outcome is null)
        {
            outcome = new RecommendationOutcome { RecommendationId = rec.Id };
            db.RecommendationOutcomes.Add(outcome);
            rec.Outcome = outcome;
        }

        outcome.Status = result.Status;
        outcome.EntryDate = result.EntryDate;
        outcome.EntryPrice = result.EntryPrice;
        outcome.ExitDate = result.ExitDate;
        outcome.ExitPrice = result.ExitPrice;
        outcome.ReturnPercent = result.ReturnPercent;
        outcome.RMultiple = result.RMultiple;
        outcome.MaxFavorablePercent = result.MaxFavorablePercent;
        outcome.MaxAdversePercent = result.MaxAdversePercent;
        outcome.DaysHeld = result.DaysHeld;
        outcome.BenchmarkReturnPercent = result.BenchmarkReturnPercent;
        outcome.GraderVersion = Grader.Version;
        outcome.GradedAtUtc = graded ? clock.UtcNow : null;
    }
}
