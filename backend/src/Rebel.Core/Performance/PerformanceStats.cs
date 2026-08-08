using Rebel.Core.Domain;

namespace Rebel.Core.Performance;

/// <summary>A resolved recommendation flattened for analytics.</summary>
public sealed record GradedTrade
{
    public required string Symbol { get; init; }
    public required string Strategy { get; init; }
    public required ConfidenceLevel Confidence { get; init; }
    public required OutcomeStatus Status { get; init; }
    public required DateOnly TradingDate { get; init; }
    public decimal ReturnPercent { get; init; }
    public decimal? RMultiple { get; init; }
    public int DaysHeld { get; init; }
    public decimal? BenchmarkReturnPercent { get; init; }

    /// <summary>Ideas that never triggered say nothing about signal quality, so they are excluded from the stats.</summary>
    public bool Counts => Status is OutcomeStatus.TargetHit or OutcomeStatus.StopHit or OutcomeStatus.Expired;
}

public sealed record PerformanceStats
{
    public string Segment { get; init; } = "all";
    public int Trades { get; init; }
    public int Wins { get; init; }
    public int Losses { get; init; }
    public int NeverTriggered { get; init; }

    public decimal WinRate { get; init; }
    public decimal AverageReturnPercent { get; init; }
    public decimal AverageWinPercent { get; init; }
    public decimal AverageLossPercent { get; init; }
    public decimal BestPercent { get; init; }
    public decimal WorstPercent { get; init; }

    /// <summary>Gross profit over gross loss. Below 1.0 the system loses money regardless of win rate.</summary>
    public decimal ProfitFactor { get; init; }

    /// <summary>Average R per trade — the number that actually decides whether the edge is real.</summary>
    public decimal Expectancy { get; init; }

    public decimal AverageHoldingDays { get; init; }
    public decimal AverageBenchmarkPercent { get; init; }

    /// <summary>Average return minus average benchmark return over the same windows.</summary>
    public decimal AlphaPercent { get; init; }

    /// <summary>Compounded return of taking every idea at equal weight, in sequence.</summary>
    public decimal CompoundedReturnPercent { get; init; }

    public static PerformanceStats Empty(string segment = "all") => new() { Segment = segment };

    public static PerformanceStats From(IEnumerable<GradedTrade> trades, string segment = "all")
    {
        ArgumentNullException.ThrowIfNull(trades);

        var all = trades.ToArray();
        var counted = all.Where(t => t.Counts).OrderBy(t => t.TradingDate).ToArray();
        var neverTriggered = all.Length - counted.Length;

        if (counted.Length == 0) return Empty(segment) with { NeverTriggered = neverTriggered };

        var wins = counted.Where(t => t.ReturnPercent > 0m).ToArray();
        var losses = counted.Where(t => t.ReturnPercent <= 0m).ToArray();

        var grossProfit = wins.Sum(t => t.ReturnPercent);
        var grossLoss = Math.Abs(losses.Sum(t => t.ReturnPercent));

        var compounded = 1m;
        foreach (var t in counted) compounded *= 1m + t.ReturnPercent / 100m;

        var withBenchmark = counted.Where(t => t.BenchmarkReturnPercent.HasValue).ToArray();
        var avgBenchmark = withBenchmark.Length == 0
            ? 0m
            : withBenchmark.Average(t => t.BenchmarkReturnPercent!.Value);

        var avgReturn = counted.Average(t => t.ReturnPercent);
        var withR = counted.Where(t => t.RMultiple.HasValue).ToArray();

        return new PerformanceStats
        {
            Segment = segment,
            Trades = counted.Length,
            Wins = wins.Length,
            Losses = losses.Length,
            NeverTriggered = neverTriggered,
            WinRate = Round(wins.Length / (decimal)counted.Length * 100m),
            AverageReturnPercent = Round(avgReturn),
            AverageWinPercent = wins.Length == 0 ? 0m : Round(wins.Average(t => t.ReturnPercent)),
            AverageLossPercent = losses.Length == 0 ? 0m : Round(losses.Average(t => t.ReturnPercent)),
            BestPercent = Round(counted.Max(t => t.ReturnPercent)),
            WorstPercent = Round(counted.Min(t => t.ReturnPercent)),
            // No losses yet is not infinite edge; report the gross profit instead of dividing by zero.
            ProfitFactor = grossLoss == 0m ? Round(grossProfit) : Round(grossProfit / grossLoss),
            Expectancy = withR.Length == 0 ? 0m : Round(withR.Average(t => t.RMultiple!.Value)),
            AverageHoldingDays = Round((decimal)counted.Average(t => t.DaysHeld)),
            AverageBenchmarkPercent = Round(avgBenchmark),
            AlphaPercent = Round(avgReturn - avgBenchmark),
            CompoundedReturnPercent = Round((compounded - 1m) * 100m)
        };
    }

    /// <summary>Same stats, split by any dimension — strategy, confidence, holding period bucket.</summary>
    public static IReadOnlyList<PerformanceStats> Breakdown<TKey>(
        IEnumerable<GradedTrade> trades,
        Func<GradedTrade, TKey> keySelector,
        Func<TKey, string>? nameSelector = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(trades);
        ArgumentNullException.ThrowIfNull(keySelector);

        return trades
            .GroupBy(keySelector)
            .Select(g => From(g, nameSelector?.Invoke(g.Key) ?? g.Key.ToString() ?? "unknown"))
            .OrderByDescending(s => s.Trades)
            .ToArray();
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
