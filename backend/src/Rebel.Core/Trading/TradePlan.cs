using Rebel.Core.Domain;

namespace Rebel.Core.Trading;

public sealed record TradePlan
{
    public required string Symbol { get; init; }
    public TradeDirection Direction { get; init; } = TradeDirection.Long;

    public required decimal ReferencePrice { get; init; }
    public required decimal EntryLow { get; init; }
    public required decimal EntryHigh { get; init; }
    public required decimal Target { get; init; }
    public required decimal StopLoss { get; init; }

    public required decimal RiskRewardRatio { get; init; }
    public required decimal StopDistancePercent { get; init; }
    public required decimal TargetDistancePercent { get; init; }

    public required int SuggestedShares { get; init; }
    public required decimal SuggestedRiskAmount { get; init; }
    public required int HoldingPeriodDays { get; init; }

    public decimal EntryMidpoint => (EntryLow + EntryHigh) / 2m;
    public decimal PositionCost => EntryMidpoint * SuggestedShares;
}

public sealed record RiskSettings
{
    /// <summary>Simulated account equity used for position sizing.</summary>
    public decimal AccountEquity { get; init; } = 100_000m;

    /// <summary>Percent of equity risked between entry and stop on a single idea.</summary>
    public decimal RiskPerTradePercent { get; init; } = 1.0m;

    /// <summary>Cap on any one position as a percent of equity, regardless of stop distance.</summary>
    public decimal MaxPositionPercent { get; init; } = 20m;

    /// <summary>Preferred stop distance in ATRs, used when the swing low is uninformative.</summary>
    public decimal StopAtrMultiple { get; init; } = 1.5m;

    /// <summary>Never risk less than this many ATRs — a tighter stop is just noise-triggered.</summary>
    public decimal MinStopAtrMultiple { get; init; } = 1.0m;

    /// <summary>Never risk more than this many ATRs, however far away the swing low sits.</summary>
    public decimal MaxStopAtrMultiple { get; init; } = 2.5m;

    /// <summary>Floor on the target distance in ATRs when the breakout level is close by.</summary>
    public decimal MinTargetAtrMultiple { get; init; } = 2.0m;

    /// <summary>
    /// Ceiling on target distance in ATRs. Beyond this the move required inside a
    /// swing holding window stops being plausible, and the setup is rejected.
    /// </summary>
    public decimal MaxTargetAtrMultiple { get; init; } = 6.0m;

    /// <summary>How far past the 20-day high the breakout objective is placed, in ATRs.</summary>
    public decimal BreakoutExtensionAtrMultiple { get; init; } = 0.5m;

    /// <summary>Clearance below the swing low, in ATRs, so the stop is not sitting on the obvious level.</summary>
    public decimal SwingLowBufferAtrMultiple { get; init; } = 0.25m;

    /// <summary>Half-width of the entry range, in ATRs.</summary>
    public decimal EntryBandAtrMultiple { get; init; } = 0.25m;

    /// <summary>Reject any plan that cannot offer at least this much reward per unit of risk.</summary>
    public decimal MinimumRiskReward { get; init; } = 1.75m;

    public int MinHoldingDays { get; init; } = 1;
    public int MaxHoldingDays { get; init; } = 10;
}
