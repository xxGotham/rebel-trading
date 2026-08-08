namespace Rebel.Core.Domain;

/// <summary>
/// What actually happened. Written by the grader once the holding window closes
/// (or earlier, if target/stop resolved first).
/// </summary>
public class RecommendationOutcome
{
    public long Id { get; set; }
    public Guid RecommendationId { get; set; }
    public Recommendation? Recommendation { get; set; }

    public OutcomeStatus Status { get; set; }
    public DateTime? GradedAtUtc { get; set; }

    /// <summary>Session on which the idea first traded into the entry range.</summary>
    public DateOnly? EntryDate { get; set; }
    public decimal? EntryPrice { get; set; }

    public DateOnly? ExitDate { get; set; }
    public decimal? ExitPrice { get; set; }

    public decimal? ReturnPercent { get; set; }

    /// <summary>Return in units of initial risk (entry − stop). The honest measure for a stop-based system.</summary>
    public decimal? RMultiple { get; set; }

    /// <summary>Maximum favourable / adverse excursion while the trade was open.</summary>
    public decimal? MaxFavorablePercent { get; set; }
    public decimal? MaxAdversePercent { get; set; }

    public int? DaysHeld { get; set; }

    /// <summary>Benchmark return over the identical window, for alpha attribution.</summary>
    public decimal? BenchmarkReturnPercent { get; set; }

    public bool IsWin => ReturnPercent > 0m;

    /// <summary>Grader version, so a change in grading rules invalidates comparisons visibly.</summary>
    public string GraderVersion { get; set; } = string.Empty;
}
