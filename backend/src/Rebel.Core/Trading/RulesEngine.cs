using Rebel.Core.Domain;

namespace Rebel.Core.Trading;

public interface IRulesEngine
{
    IReadOnlyList<TradingEvent> Evaluate(
        Guid sessionId,
        MarketState current,
        MarketState? previous,
        Playbook playbook);
}

public class RulesEngine : IRulesEngine
{
    public IReadOnlyList<TradingEvent> Evaluate(
        Guid sessionId,
        MarketState current,
        MarketState? previous,
        Playbook playbook)
    {
        var events = new List<TradingEvent>();

        // Position opened/closed detection
        if (current.CurrentPosition != null && previous?.CurrentPosition == null)
            events.Add(Build(sessionId, TradingEventType.PositionOpened,
                $"Position opened: {current.CurrentPosition.Side} {current.CurrentPosition.Quantity} {current.Instrument} @ {current.CurrentPosition.EntryPrice:F2}",
                new { current.CurrentPosition }));

        if (current.CurrentPosition == null && previous?.CurrentPosition != null)
            events.Add(Build(sessionId, TradingEventType.PositionClosed,
                $"Position closed on {current.Instrument}. Session P&L: {current.SessionPnl:C}",
                new { previous.CurrentPosition, SessionPnl = current.SessionPnl }));

        if (current.CurrentPosition != null && previous?.CurrentPosition != null)
        {
            var pos = current.CurrentPosition;
            var prev = previous.CurrentPosition;

            // Stop changed
            if (pos.StopPrice != prev.StopPrice && pos.StopPrice.HasValue)
                events.Add(Build(sessionId, TradingEventType.StopChanged,
                    $"Stop moved to {pos.StopPrice:F2}",
                    new { OldStop = prev.StopPrice, NewStop = pos.StopPrice }));

            // Target changed
            if (pos.TargetPrice != prev.TargetPrice && pos.TargetPrice.HasValue)
                events.Add(Build(sessionId, TradingEventType.TargetChanged,
                    $"Target moved to {pos.TargetPrice:F2}",
                    new { OldTarget = prev.TargetPrice, NewTarget = pos.TargetPrice }));

            // Stop management threshold (Playbook-driven)
            if (playbook.Rules.StopManagementRules.Count > 0 && pos.StopPrice.HasValue)
            {
                var pnlPts = pos.UnrealizedPnlPts ?? 0;
                // Trigger at +30 points (futures) or +2% (stocks)
                var threshold = playbook.AssetClass == AssetClass.Futures ? 30m : 0.02m;
                var prevPnlPts = prev.UnrealizedPnlPts ?? 0;
                if (pnlPts >= threshold && prevPnlPts < threshold)
                    events.Add(Build(sessionId, TradingEventType.StopThresholdReached,
                        $"You're {pnlPts:F2} points above entry. Your first stop management threshold has been reached.",
                        new { PnlPts = pnlPts, Threshold = threshold }));
            }

            // Profit target approaching
            if (pos.TargetPrice.HasValue && current.LatestQuote != null)
            {
                var price = current.LatestQuote.Price;
                var distanceToTarget = Math.Abs(pos.TargetPrice.Value - price);
                var totalDistance = Math.Abs(pos.TargetPrice.Value - pos.EntryPrice);
                if (totalDistance > 0)
                {
                    var pct = 1 - (distanceToTarget / totalDistance);
                    var prevPrice = previous.LatestQuote?.Price ?? price;
                    var prevDistanceToTarget = Math.Abs(pos.TargetPrice.Value - prevPrice);
                    var prevPct = 1 - (prevDistanceToTarget / totalDistance);
                    if (pct >= 0.8m && prevPct < 0.8m)
                        events.Add(Build(sessionId, TradingEventType.ProfitTargetApproaching,
                            $"Price is approaching your profit target at {pos.TargetPrice:F2}.",
                            new { TargetPrice = pos.TargetPrice, CurrentPrice = price }));
                }
            }

            // Max daily loss
            if (playbook.Rules.MaxDailyRisk.HasValue && current.DailyLoss > 0)
            {
                var limitReached = current.DailyLoss >= playbook.Rules.MaxDailyRisk.Value;
                var prevLimitReached = (previous?.DailyLoss ?? 0) >= playbook.Rules.MaxDailyRisk.Value;
                if (limitReached && !prevLimitReached)
                    events.Add(Build(sessionId, TradingEventType.MaxDailyLossReached,
                        $"Daily loss limit of {playbook.Rules.MaxDailyRisk.Value}% reached. Consider ending your session.",
                        new { DailyLoss = current.DailyLoss }));
            }

            // Max trades
            if (playbook.Rules.MaxTradesPerDay.HasValue)
            {
                if (current.TradeCount >= playbook.Rules.MaxTradesPerDay.Value
                    && (previous?.TradeCount ?? 0) < playbook.Rules.MaxTradesPerDay.Value)
                    events.Add(Build(sessionId, TradingEventType.MaxTradeCountReached,
                        $"You've reached your maximum of {playbook.Rules.MaxTradesPerDay.Value} trades for today.",
                        new { TradeCount = current.TradeCount }));
            }
        }

        return events;
    }

    private static TradingEvent Build(Guid sessionId, TradingEventType type, string description, object? data = null)
    {
        var ev = new TradingEvent { SessionId = sessionId, EventType = type, Description = description };
        if (data != null)
            foreach (var prop in data.GetType().GetProperties())
                ev.Data[prop.Name] = prop.GetValue(data) ?? "null";
        return ev;
    }
}
