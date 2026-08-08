using Rebel.Core.Domain;
using Rebel.Core.Signals;

namespace Rebel.Core.Trading;

public sealed record TradePlanResult(TradePlan? Plan, string? Rejection)
{
    public bool Success => Plan is not null;
}

/// <summary>
/// Builds the numeric half of a recommendation: entry band, stop, target, size,
/// holding period.
/// <para>
/// All of it is ATR-derived, which is the point. Volatility is the only sensible
/// unit for a stop — a fixed "3% stop" is a coin flip on one name and a
/// guaranteed stop-out on another. The LLM is never asked for these numbers and
/// never gets a chance to adjust them.
/// </para>
/// </summary>
public sealed class TradePlanBuilder
{
    private readonly RiskSettings _risk;

    public TradePlanBuilder(RiskSettings? risk = null) => _risk = risk ?? new RiskSettings();

    public RiskSettings Risk => _risk;

    public TradePlanResult Build(SignalSet signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var price = signals.LastPrice;
        var atr = (decimal)signals.Atr14;

        if (price <= 0m) return new TradePlanResult(null, "no valid reference price");
        if (atr <= 0m) return new TradePlanResult(null, "ATR unavailable — cannot size risk");

        var band = Round(atr * _risk.EntryBandAtrMultiple);
        var entryLow = Round(price - band);
        var entryHigh = Round(price + band);
        var entryMid = Round((entryLow + entryHigh) / 2m);

        var stop = Round(BuildStop(signals, entryMid, atr));
        if (stop <= 0m) return new TradePlanResult(null, "stop would fall at or below zero");

        var riskPerShare = entryMid - stop;
        if (riskPerShare <= 0m) return new TradePlanResult(null, "stop is not below entry");

        var target = Round(BuildTarget(signals, entryMid, atr, riskPerShare));

        // The binding constraint is plausibility, not arithmetic. Because the
        // target floor is expressed in units of risk, a setup only fails when the
        // stop is so wide that clearing the minimum risk/reward would demand a
        // move this name has no business making inside a swing window.
        var atrsToTarget = (target - entryMid) / atr;
        if (atrsToTarget > _risk.MaxTargetAtrMultiple)
            return new TradePlanResult(null,
                $"a {_risk.MinimumRiskReward:F2}:1 risk/reward target sits {atrsToTarget:F1} ATRs away — " +
                $"beyond the {_risk.MaxTargetAtrMultiple:F1} ATR limit for a swing hold");

        var rewardPerShare = target - entryMid;
        var riskReward = Math.Round(rewardPerShare / riskPerShare, 2);

        if (riskReward < _risk.MinimumRiskReward)
            return new TradePlanResult(null,
                $"risk/reward {riskReward:F2} below minimum {_risk.MinimumRiskReward:F2}");

        var riskBudget = _risk.AccountEquity * (_risk.RiskPerTradePercent / 100m);
        var shares = (int)Math.Floor(riskBudget / riskPerShare);

        // A wide stop on an expensive name can size into an absurd notional; the
        // position cap binds before the risk budget does.
        var maxNotional = _risk.AccountEquity * (_risk.MaxPositionPercent / 100m);
        if (shares > 0 && entryMid * shares > maxNotional)
            shares = (int)Math.Floor(maxNotional / entryMid);

        if (shares <= 0)
            return new TradePlanResult(null, "position size rounds to zero shares at this risk budget");

        var plan = new TradePlan
        {
            Symbol = signals.Symbol,
            Direction = TradeDirection.Long,
            ReferencePrice = price,
            EntryLow = entryLow,
            EntryHigh = entryHigh,
            Target = target,
            StopLoss = stop,
            RiskRewardRatio = riskReward,
            StopDistancePercent = Math.Round(riskPerShare / entryMid * 100m, 2),
            TargetDistancePercent = Math.Round(rewardPerShare / entryMid * 100m, 2),
            SuggestedShares = shares,
            SuggestedRiskAmount = Math.Round(riskPerShare * shares, 2),
            HoldingPeriodDays = EstimateHoldingPeriod(rewardPerShare, atr)
        };

        var invalid = TradePlanValidator.Validate(plan, _risk);
        return invalid is null ? new TradePlanResult(plan, null) : new TradePlanResult(null, invalid);
    }

    /// <summary>
    /// Stop placement is structural, not arithmetic: it belongs just under the
    /// level that would prove the trend wrong — the recent swing low — with a
    /// buffer so it isn't resting on the price everyone else is watching. The
    /// ATR bounds stop that structure from producing a stop so tight it's noise
    /// or so wide the position can't be sized.
    /// </summary>
    private decimal BuildStop(SignalSet signals, decimal entryMid, decimal atr)
    {
        var structural = signals.Low10Day > 0m
            ? signals.Low10Day - atr * _risk.SwingLowBufferAtrMultiple
            : entryMid - atr * _risk.StopAtrMultiple;

        var tightest = entryMid - atr * _risk.MinStopAtrMultiple;
        var widest = entryMid - atr * _risk.MaxStopAtrMultiple;

        return Math.Clamp(structural, widest, tightest);
    }

    /// <summary>
    /// Target is the breakout objective — clear of the 20-day high, where supply
    /// has repeatedly shown up — raised to whichever floor binds: a minimum
    /// distance in ATRs, or the distance needed to make the trade worth its risk.
    /// <para>
    /// Expressing the second floor in units of risk is what keeps risk/reward
    /// meaningful. A fixed ATR target against a structural stop produces a ratio
    /// that varies only with stop width, so a name sitting on its high with a
    /// wide swing low would be rejected for a reason that says nothing about the
    /// setup. Here the ratio still varies — a distant breakout level lifts the
    /// target well above the floor — but a poor one is expressed as an
    /// implausible required move rather than an arithmetic artefact.
    /// </para>
    /// </summary>
    private decimal BuildTarget(SignalSet signals, decimal entryMid, decimal atr, decimal riskPerShare)
    {
        var breakout = signals.High20Day > 0m
            ? signals.High20Day + atr * _risk.BreakoutExtensionAtrMultiple
            : entryMid + atr * _risk.MinTargetAtrMultiple;

        var volatilityFloor = entryMid + atr * _risk.MinTargetAtrMultiple;
        var riskFloor = entryMid + riskPerShare * _risk.MinimumRiskReward;

        return Math.Max(breakout, Math.Max(volatilityFloor, riskFloor));
    }

    /// <summary>
    /// How long the target should take at the name's own pace: distance to target
    /// in ATRs, assuming roughly half an ATR of net progress per session.
    /// </summary>
    private int EstimateHoldingPeriod(decimal rewardPerShare, decimal atr)
    {
        if (atr <= 0m) return _risk.MinHoldingDays;

        var atrsToTarget = (double)(rewardPerShare / atr);
        var estimate = (int)Math.Ceiling(atrsToTarget / 0.5);
        return Math.Clamp(estimate, _risk.MinHoldingDays, _risk.MaxHoldingDays);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public static class TradePlanValidator
{
    /// <summary>Returns null when the plan is coherent, otherwise the reason it isn't.</summary>
    public static string? Validate(TradePlan plan, RiskSettings risk)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(risk);

        if (plan.Direction != TradeDirection.Long) return "only long plans are supported in V1";
        if (plan.EntryLow <= 0m || plan.EntryHigh <= 0m) return "entry range must be positive";
        if (plan.EntryLow > plan.EntryHigh) return "entry range is inverted";
        if (plan.StopLoss >= plan.EntryLow) return "stop must sit below the entry range";
        if (plan.Target <= plan.EntryHigh) return "target must sit above the entry range";
        if (plan.RiskRewardRatio < risk.MinimumRiskReward) return "risk/reward below minimum";
        if (plan.SuggestedShares <= 0) return "position size must be at least one share";
        if (plan.HoldingPeriodDays < risk.MinHoldingDays || plan.HoldingPeriodDays > risk.MaxHoldingDays)
            return "holding period outside the configured swing window";

        var maxNotional = risk.AccountEquity * (risk.MaxPositionPercent / 100m);
        if (plan.PositionCost > maxNotional) return "position exceeds the maximum position size";

        return null;
    }
}
