using Hangfire;
using Rebel.Core.Domain;
using Rebel.Infrastructure.Pipeline;

namespace Rebel.Api.Jobs;

/// <summary>
/// Background work. Both jobs call exactly the same services the HTTP endpoints
/// do — the schedule is a trigger, never a second implementation.
/// </summary>
public sealed class RebelJobs(
    AnalysisPipeline pipeline,
    GradingService grading,
    ILogger<RebelJobs> log)
{
    public const string MorningRunId = "rebel-morning-run";
    public const string GradingId = "rebel-grading";

    /// <summary>Pre-market run: produces the morning brief before the open.</summary>
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [120, 300])]
    public async Task MorningRunAsync(CancellationToken ct)
    {
        log.LogInformation("Scheduled morning run starting");

        var run = await pipeline.RunAsync(new RunRequest { Trigger = RunTrigger.Scheduled }, ct);

        log.LogInformation("Scheduled morning run {RunId} finished with {Status}: {Count} recommendations",
            run.Id, run.Status, run.RecommendationCount);

        // Surfacing failure to Hangfire is what makes the retry policy above real.
        if (run.Status == RunStatus.Failed)
            throw new InvalidOperationException($"Morning run failed: {run.ErrorMessage}");
    }

    /// <summary>Post-close grading of every unresolved recommendation.</summary>
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900])]
    public async Task GradeAsync(CancellationToken ct)
    {
        var summary = await grading.GradeOutstandingAsync(ct);
        log.LogInformation("Scheduled grading finished: {Summary}", summary);
    }
}
