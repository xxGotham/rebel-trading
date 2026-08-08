namespace Rebel.Core.Domain;

public enum RunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    /// <summary>Pipeline finished but produced no recommendations (e.g. nothing cleared the score floor).</summary>
    CompletedEmpty = 4
}

public enum RunTrigger
{
    Manual = 0,
    Scheduled = 1,
    Backfill = 2
}

public enum TradeDirection
{
    Long = 0,
    Short = 1
}

public enum ConfidenceLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum OutcomeStatus
{
    /// <summary>Rec is inside its holding window and has not resolved yet.</summary>
    Open = 0,
    TargetHit = 1,
    StopHit = 2,
    /// <summary>Holding period elapsed without hitting target or stop; graded at the close.</summary>
    Expired = 3,
    /// <summary>Price never traded into the entry range, so the idea was never actionable.</summary>
    NeverTriggered = 4
}

public enum PaperPositionStatus
{
    Open = 0,
    Closed = 1
}
