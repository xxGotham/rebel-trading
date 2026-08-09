using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Rebel.Core.Abstractions;
using Rebel.Core.Domain;
using Rebel.Core.Trading;

namespace Rebel.Infrastructure.Services;

public sealed class ActiveSession
{
    public required TradingSession Session { get; init; }
    public required Playbook Playbook { get; init; }
    public MarketState MarketState { get; set; } = new();
    public CancellationTokenSource Cts { get; } = new();
}

public interface ICoachingSessionService
{
    Task<TradingSession> StartAsync(string userId, string userName, Playbook playbook, string instrument, CancellationToken ct = default);
    Task EndAsync(Guid sessionId, CancellationToken ct = default);
    ActiveSession? GetActive(Guid sessionId);
    Task<string> AskAsync(Guid sessionId, string userId, string question, CancellationToken ct = default);
}

public sealed class CoachingSessionService<THub>(
    IHubContext<THub> hubContext,
    IMarketDataProvider marketData,
    IBrokerProvider broker,
    IAIProvider ai,
    IRulesEngine rules,
    ILogger<CoachingSessionService<THub>> logger)
    : ICoachingSessionService
    where THub : Hub
{
    private readonly Dictionary<Guid, ActiveSession> _sessions = new();

    public Task<TradingSession> StartAsync(string userId, string userName, Playbook playbook, string instrument, CancellationToken ct = default)
    {
        var session = new TradingSession
        {
            UserId = userId,
            PlaybookId = playbook.Id,
            PlaybookName = playbook.Name,
            Instrument = instrument,
            AssetClass = playbook.AssetClass,
            Status = SessionStatus.Active,
        };
        var active = new ActiveSession { Session = session, Playbook = playbook, MarketState = new() { Instrument = instrument } };
        _sessions[session.Id] = active;
        _ = RunMarketLoopAsync(active, userName);
        return Task.FromResult(session);
    }

    public Task EndAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var a))
        {
            a.Cts.Cancel();
            a.Session.Status = SessionStatus.Ended;
            a.Session.EndedAt = DateTimeOffset.UtcNow;
            _sessions.Remove(sessionId);
        }
        return Task.CompletedTask;
    }

    public ActiveSession? GetActive(Guid sessionId) =>
        _sessions.TryGetValue(sessionId, out var s) ? s : null;

    public async Task<string> AskAsync(Guid sessionId, string userId, string question, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var active))
            return "No active session.";

        var ctx = BuildContext(active);
        var answer = await ai.AnswerQuestionAsync(ctx, question, ct);

        var msg = new ConversationMessage { SessionId = sessionId, Text = answer, IsUserQuestion = false };
        active.Session.Messages.Add(msg);

        await hubContext.Clients.Group(sessionId.ToString()).SendAsync("RebelSpeaking", new
        {
            id = msg.Id.ToString(),
            text = msg.Text,
            timestamp = msg.Timestamp,
            isUserQuestion = false,
        }, ct);

        return answer;
    }

    private async Task RunMarketLoopAsync(ActiveSession active, string userName)
    {
        var ct = active.Cts.Token;
        var groupId = active.Session.Id.ToString();
        MarketState? prev = null;

        try
        {
            await foreach (var _ in marketData.StreamTicksAsync(active.Session.Instrument, ct))
            {
                var quote = await marketData.GetQuoteAsync(active.Session.Instrument, ct);
                if (quote != null) active.MarketState.LatestQuote = quote;

                var position = await broker.GetPositionAsync(active.Session.Instrument, ct);
                active.MarketState.CurrentPosition = position;
                active.MarketState.OpenOrders = [..await broker.GetOpenOrdersAsync(active.Session.Instrument, ct)];
                active.MarketState.LastUpdated = DateTimeOffset.UtcNow;

                await hubContext.Clients.Group(groupId).SendAsync("QuoteUpdated", MapQuote(quote), ct);
                await hubContext.Clients.Group(groupId).SendAsync("PositionUpdated", MapPosition(position), ct);

                if (prev != null && (active.MarketState.LastUpdated - prev.LastUpdated).TotalSeconds >= 2)
                {
                    foreach (var ev in rules.Evaluate(active.Session.Id, active.MarketState, prev, active.Playbook))
                    {
                        active.Session.Events.Add(ev);
                        var coaching = await ai.GenerateCoachingMessageAsync(BuildContext(active), ev, ct);
                        var msg = new ConversationMessage { SessionId = active.Session.Id, Text = coaching, IsUserQuestion = false, RelatedEventType = ev.EventType };
                        active.Session.Messages.Add(msg);

                        await hubContext.Clients.Group(groupId).SendAsync("RebelSpeaking", new
                        {
                            id = msg.Id.ToString(),
                            text = coaching,
                            timestamp = msg.Timestamp,
                            eventType = ev.EventType.ToString(),
                            isUserQuestion = false,
                        }, ct);
                    }
                }

                prev = Clone(active.MarketState);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogError(ex, "Market loop failed for session {Id}", active.Session.Id); }
    }

    private static AIContext BuildContext(ActiveSession a) => new(
        a.Session.UserId, a.Playbook, a.MarketState,
        a.Session.Events.TakeLast(5).ToList(),
        a.Session.Messages.TakeLast(10).ToList());

    private static object? MapQuote(Quote? q) => q == null ? null : new
    {
        instrument = q.Instrument, price = q.Price, bid = q.Bid, ask = q.Ask,
        change = q.Change, changePct = q.ChangePct, timestamp = q.Timestamp,
    };

    private static object? MapPosition(LivePosition? p) => p == null ? null : new
    {
        instrument = p.Instrument, assetClass = p.AssetClass.ToString(), side = p.Side.ToString(),
        quantity = p.Quantity, entryPrice = p.EntryPrice, currentPrice = p.CurrentPrice,
        stopPrice = p.StopPrice, targetPrice = p.TargetPrice,
        unrealizedPnl = p.UnrealizedPnl, unrealizedPnlPts = p.UnrealizedPnlPts, realizedPnl = p.RealizedPnl,
    };

    private static MarketState Clone(MarketState s) => new()
    {
        Instrument = s.Instrument, LatestQuote = s.LatestQuote, CurrentPosition = s.CurrentPosition,
        OpenOrders = [..s.OpenOrders], SessionPnl = s.SessionPnl, TradeCount = s.TradeCount,
        DailyLoss = s.DailyLoss, LastUpdated = s.LastUpdated,
    };
}
