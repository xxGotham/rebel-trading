using Rebel.Core.Trading;
using Xunit;

namespace Rebel.Core.Tests;

public class TradePlanTests
{
    [Fact]
    public void A_plan_brackets_the_entry_with_a_stop_below_and_a_target_above()
    {
        var builder = new TradePlanBuilder();

        var plan = builder.Build(TestData.Signals(lastPrice: 100m, atr: 2d)).Plan;

        Assert.NotNull(plan);
        Assert.True(plan.StopLoss < plan.EntryLow, "stop must sit below the entry band");
        Assert.True(plan.EntryLow < plan.EntryHigh, "entry band must be ordered");
        Assert.True(plan.Target > plan.EntryHigh, "target must sit above the entry band");
    }

    [Fact]
    public void Risk_reward_varies_with_the_setup_rather_than_being_a_constant()
    {
        // Fixed ATR multiples on both ends would make this ratio identical for
        // every candidate, which would carry no information and would make the
        // minimum-risk/reward filter incapable of rejecting anything.
        var builder = new TradePlanBuilder();

        var nearHigh = builder.Build(TestData.Signals(
            lastPrice: 100m, atr: 2d, high20: 101m, low10: 97m)).Plan;

        var farFromHigh = builder.Build(TestData.Signals(
            lastPrice: 100m, atr: 2d, high20: 108m, low10: 97m)).Plan;

        Assert.NotNull(nearHigh);
        Assert.NotNull(farFromHigh);
        Assert.NotEqual(nearHigh.RiskRewardRatio, farFromHigh.RiskRewardRatio);
        Assert.True(farFromHigh.RiskRewardRatio > nearHigh.RiskRewardRatio,
            "a more distant breakout level should offer more reward per unit of risk");
    }

    [Fact]
    public void The_stop_sits_below_the_recent_swing_low()
    {
        var builder = new TradePlanBuilder();

        var plan = builder.Build(TestData.Signals(lastPrice: 100m, atr: 2d, low10: 97m)).Plan;

        Assert.NotNull(plan);
        Assert.True(plan.StopLoss < 97m, "the stop belongs below the level that would invalidate the trend");
    }

    [Fact]
    public void The_stop_is_never_wider_than_the_configured_atr_ceiling()
    {
        var builder = new TradePlanBuilder();

        // A swing low far below would otherwise produce a stop too wide to size.
        var plan = builder.Build(TestData.Signals(lastPrice: 100m, atr: 2d, low10: 60m)).Plan;

        Assert.NotNull(plan);
        Assert.True(plan.EntryMidpoint - plan.StopLoss <= 2m * 2.5m + 0.01m);
    }

    [Fact]
    public void The_stop_is_never_tighter_than_the_configured_atr_floor()
    {
        var builder = new TradePlanBuilder();

        // A swing low just under the price would give a stop inside the noise.
        var plan = builder.Build(TestData.Signals(lastPrice: 100m, atr: 2d, low10: 99.9m)).Plan;

        Assert.NotNull(plan);
        Assert.True(plan.EntryMidpoint - plan.StopLoss >= 2m * 1.0m - 0.01m);
    }

    [Fact]
    public void Position_size_respects_the_risk_budget()
    {
        var risk = new RiskSettings { AccountEquity = 100_000m, RiskPerTradePercent = 1m };
        var builder = new TradePlanBuilder(risk);

        var plan = builder.Build(TestData.Signals(lastPrice: 100m, atr: 2d, low10: 97m)).Plan;

        Assert.NotNull(plan);
        Assert.True(plan.SuggestedRiskAmount <= 1_000m,
            $"risked {plan.SuggestedRiskAmount} against a 1,000 budget");
    }

    [Fact]
    public void Position_size_is_capped_by_the_maximum_position_even_when_risk_allows_more()
    {
        // A very tight stop would otherwise size into an enormous notional.
        var risk = new RiskSettings
        {
            AccountEquity = 100_000m,
            RiskPerTradePercent = 5m,
            MaxPositionPercent = 20m
        };
        var builder = new TradePlanBuilder(risk);

        var plan = builder.Build(TestData.Signals(lastPrice: 50m, atr: 0.5d, low10: 49.5m)).Plan;

        Assert.NotNull(plan);
        Assert.True(plan.PositionCost <= 20_000m, $"position cost {plan.PositionCost} exceeded the cap");
    }

    [Fact]
    public void A_setup_that_cannot_clear_the_minimum_risk_reward_is_rejected()
    {
        var risk = new RiskSettings { MinimumRiskReward = 5m };
        var builder = new TradePlanBuilder(risk);

        var result = builder.Build(TestData.Signals(lastPrice: 100m, atr: 2d, high20: 101m, low10: 97m));

        Assert.Null(result.Plan);
        Assert.Contains("risk/reward", result.Rejection);
    }

    [Fact]
    public void A_signal_set_with_no_volatility_estimate_is_rejected()
    {
        var result = new TradePlanBuilder().Build(TestData.Signals(atr: 0d));

        Assert.Null(result.Plan);
        Assert.Contains("ATR", result.Rejection);
    }

    [Fact]
    public void Holding_period_stays_inside_the_configured_swing_window()
    {
        var builder = new TradePlanBuilder();

        var quick = builder.Build(TestData.Signals(lastPrice: 100m, atr: 2d, high20: 101m)).Plan;
        var distant = builder.Build(TestData.Signals(lastPrice: 100m, atr: 2d, high20: 108m)).Plan;

        Assert.NotNull(quick);
        Assert.NotNull(distant);
        Assert.InRange(quick.HoldingPeriodDays, 1, 10);
        Assert.InRange(distant.HoldingPeriodDays, 1, 10);
        Assert.True(distant.HoldingPeriodDays >= quick.HoldingPeriodDays,
            "a more distant target should not resolve sooner");
    }

    [Fact]
    public void The_validator_rejects_an_incoherent_plan()
    {
        var risk = new RiskSettings();
        var coherent = new TradePlanBuilder(risk).Build(TestData.Signals()).Plan!;

        Assert.Null(TradePlanValidator.Validate(coherent, risk));

        Assert.NotNull(TradePlanValidator.Validate(
            coherent with { StopLoss = coherent.EntryHigh + 1m }, risk));
        Assert.NotNull(TradePlanValidator.Validate(
            coherent with { Target = coherent.EntryLow - 1m }, risk));
        Assert.NotNull(TradePlanValidator.Validate(
            coherent with { EntryLow = coherent.EntryHigh + 5m }, risk));
        Assert.NotNull(TradePlanValidator.Validate(
            coherent with { SuggestedShares = 0 }, risk));
    }
}
