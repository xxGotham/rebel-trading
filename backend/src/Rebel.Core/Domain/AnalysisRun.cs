namespace Rebel.Core.Domain;

/// <summary>
/// One execution of the pipeline. Everything needed to explain — or replay — a
/// recommendation hangs off this row, including the versions of the code paths
/// that produced it. Without those versions the performance analytics in
/// <see cref="RecommendationOutcome"/> can't be attributed to anything.
/// </summary>
public class AnalysisRun
{
    public Guid Id { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public RunStatus Status { get; set; }
    public RunTrigger Trigger { get; set; }

    /// <summary>Trading session this run is analysing, in US/Eastern.</summary>
    public DateOnly TradingDate { get; set; }

    public int UniverseSize { get; set; }
    public int CandidateCount { get; set; }
    public int RecommendationCount { get; set; }

    public string ScoringVersion { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string PipelineVersion { get; set; } = string.Empty;

    /// <summary>Hash of the effective scoring weights, so weight tweaks are detectable.</summary>
    public string ConfigHash { get; set; } = string.Empty;

    public string? Model { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Human-readable trace of what each stage did, for debugging a run after the fact.</summary>
    public string StageLog { get; set; } = string.Empty;

    public List<Candidate> Candidates { get; set; } = [];
    public List<Recommendation> Recommendations { get; set; } = [];
}
