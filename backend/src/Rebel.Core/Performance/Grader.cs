using Rebel.Core.Domain;
using Rebel.Core.Market;

namespace Rebel.Core.Performance;

public sealed record GradeInput
{
    public required DateOnly TradingDate { get; init; }
    public required decimal EntryLow { get; init; }
    public required decimal EntryHigh { get; init; }
    public required decimal Target { get; init; }
    public required decimal StopLoss { get; init; }
    public required int HoldingPeriodDays { get; init; }

    /// <summary>Sessions from <see cref="TradingDate"/> forward, ascending.</summary>
    public required IReadOnlyList<DailyBar> ForwardBars { get; init; }

    public IReadOnlyList<DailyBar> BenchmarkBars { get; init; } = [];

    /// <summary>Sessions the price has to trade into the entry band before the idea is abandoned.</summary>
    public int EntryWindowDays { get; init; } = 2;
}

public sealed record GradeResult
{
    public required OutcomeStatus Status { get; init; }
    public DateOnly? EntryDate { get; init; }
    public decimal? EntryPrice { get; init; }
    public DateOnly? ExitDate { get; init; }
    public decimal? ExitPrice { get; init; }
    public decimal? ReturnPercent { get; init; }
    public decimal? RMultiple { get; init; }
    public decimal? MaxFavorablePercent { get; init; }
    public decimal? MaxAdversePercent { get; init; }
    public int? DaysHeld { get; init; }
    public decimal? BenchmarkReturnPercent { get; init; }
    public string GraderVersion { get; init; } = Grader.Version;
}

/// <summary>
/// Decides what actually happened to a recommendation.
/// <para>
/// The rules below are the whole basis of every performance number in the
/// product, so they are stated explicitly and versioned. Changing any of them
/// changes what a "win rate" means, which is why <see cref="Version"/> is
/// stamped on each outcome — a silent rule change that quietly improves the
/// track record would be the single most damaging bug this system could have.
/// </para>
/// <list type="number">
///   <item>Entry fills on the first session within the entry window whose range
///         touches the entry band, at the band edge the price came from.</item>
///   <item>Target and stop are checked on every session from the entry day
///         through the holding period.</item>
///   <item>If one bar spans both target and stop, the <b>stop</b> is assumed to
///         have hit first. Intraday sequence is unknowable from daily bars, so
///         the pessimistic reading is the honest one.</item>
///   <item>Unresolved at the end of the window: exit at that session's close.</item>
///   <item>Never trading into the band inside the entry window: the idea was
///         never actionable and is recorded as such, not as a win or a loss.</item>
/// </list>
/// </summary>
public static class Grader
{
    public const string Version = "grader-v1";

    /// <summary>Returns null when the outcome cannot be determined yet — not enough sessions have elapsed.</summary>
    public static GradeResult? Grade(GradeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var bars = input.ForwardBars
            .Where(b => b.Date >= input.TradingDate)
            .OrderBy(b => b.Date)
            .ToArray();

        if (bars.Length == 0) return null;

        var entryIndex = FindEntry(bars, input, out var entryPrice);

        if (entryIndex < 0)
        {
            // Only conclude "never triggered" once the entry window has fully elapsed.
            return bars.Length >= input.EntryWindowDays
                ? new GradeResult { Status = OutcomeStatus.NeverTriggered }
                : null;
        }

        var lastIndex = Math.Min(entryIndex + input.HoldingPeriodDays, bars.Length - 1);
        var windowComplete = entryIndex + input.HoldingPeriodDays <= bars.Length - 1;

        decimal maxHigh = entryPrice, minLow = entryPrice;

        for (var i = entryIndex; i <= lastIndex; i++)
        {
            var bar = bars[i];
            maxHigh = Math.Max(maxHigh, bar.High);
            minLow = Math.Min(minLow, bar.Low);

            var stopHit = bar.Low <= input.StopLoss;
            var targetHit = bar.High >= input.Target;

            // Pessimistic tie-break: stop wins when a single bar covers both.
            if (stopHit)
                return Resolve(input, bars, entryIndex, i, entryPrice, input.StopLoss,
                    OutcomeStatus.StopHit, maxHigh, minLow);

            if (targetHit)
                return Resolve(input, bars, entryIndex, i, entryPrice, input.Target,
                    OutcomeStatus.TargetHit, maxHigh, minLow);
        }

        if (!windowComplete) return null;

        var final = bars[lastIndex];
        return Resolve(input, bars, entryIndex, lastIndex, entryPrice, final.Close,
            OutcomeStatus.Expired, maxHigh, minLow);
    }

    private static int FindEntry(IReadOnlyList<DailyBar> bars, GradeInput input, out decimal entryPrice)
    {
        entryPrice = 0m;
        var limit = Math.Min(input.EntryWindowDays, bars.Count);

        for (var i = 0; i < limit; i++)
        {
            var bar = bars[i];
            if (bar.High < input.EntryLow || bar.Low > input.EntryHigh) continue;

            // Fill where the price actually entered the band: at the open if it
            // opened inside, otherwise at the edge it crossed.
            entryPrice = bar.Open switch
            {
                var o when o >= input.EntryLow && o <= input.EntryHigh => o,
                var o when o > input.EntryHigh => input.EntryHigh,
                _ => input.EntryLow
            };

            return i;
        }

        return -1;
    }

    private static GradeResult Resolve(
        GradeInput input,
        IReadOnlyList<DailyBar> bars,
        int entryIndex,
        int exitIndex,
        decimal entryPrice,
        decimal exitPrice,
        OutcomeStatus status,
        decimal maxHigh,
        decimal minLow)
    {
        var entryDate = bars[entryIndex].Date;
        var exitDate = bars[exitIndex].Date;
        var risk = entryPrice - input.StopLoss;

        return new GradeResult
        {
            Status = status,
            EntryDate = entryDate,
            EntryPrice = entryPrice,
            ExitDate = exitDate,
            ExitPrice = exitPrice,
            ReturnPercent = Pct(entryPrice, exitPrice),
            RMultiple = risk > 0m ? Math.Round((exitPrice - entryPrice) / risk, 3) : null,
            MaxFavorablePercent = Pct(entryPrice, maxHigh),
            MaxAdversePercent = Pct(entryPrice, minLow),
            DaysHeld = MarketCalendar.TradingDaysBetween(entryDate, exitDate),
            BenchmarkReturnPercent = BenchmarkReturn(input.BenchmarkBars, entryDate, exitDate)
        };
    }

    private static decimal? BenchmarkReturn(
        IReadOnlyList<DailyBar> benchmark, DateOnly entryDate, DateOnly exitDate)
    {
        if (benchmark.Count == 0) return null;

        var start = benchmark.Where(b => b.Date <= entryDate).OrderBy(b => b.Date).LastOrDefault();
        var end = benchmark.Where(b => b.Date <= exitDate).OrderBy(b => b.Date).LastOrDefault();

        if (start is null || end is null || start.Close == 0m) return null;
        return Pct(start.Close, end.Close);
    }

    private static decimal? Pct(decimal from, decimal to) =>
        from == 0m ? null : Math.Round((to - from) / from * 100m, 3);
}
