namespace Rebel.Core.Trading;

public record MarketTick(string Instrument, decimal Price, decimal Bid, decimal Ask, DateTimeOffset Timestamp);

public record Quote(
    string Instrument,
    decimal Price,
    decimal Bid,
    decimal Ask,
    decimal Change,
    decimal ChangePct,
    DateTimeOffset Timestamp);

public record LivePosition(
    string Instrument,
    Domain.AssetClass AssetClass,
    Domain.PositionSide Side,
    int Quantity,
    decimal EntryPrice,
    decimal CurrentPrice,
    decimal? StopPrice,
    decimal? TargetPrice,
    decimal UnrealizedPnl,
    decimal? UnrealizedPnlPts,
    decimal RealizedPnl);

public record LiveOrder(
    string Id,
    string Instrument,
    Domain.OrderSide Side,
    int Quantity,
    Domain.OrderType OrderType,
    decimal? LimitPrice,
    decimal? StopPrice,
    Domain.OrderStatus Status,
    DateTimeOffset CreatedAt);

public class MarketState
{
    public string Instrument { get; set; } = default!;
    public Quote? LatestQuote { get; set; }
    public LivePosition? CurrentPosition { get; set; }
    public List<LiveOrder> OpenOrders { get; set; } = new();
    public decimal SessionPnl { get; set; }
    public int TradeCount { get; set; }
    public decimal DailyLoss { get; set; }
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
