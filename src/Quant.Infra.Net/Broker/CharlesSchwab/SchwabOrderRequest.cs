namespace Quant.Infra.Net.Broker.CharlesSchwab;

/// <summary>
/// Schwab order request parameters.
/// Schwab 订单请求参数。
/// </summary>
public class SchwabOrderRequest
{
    /// <summary>
    /// Stock symbol (e.g., AAPL, MSFT).
    /// 股票代码。
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Order type: MARKET, LIMIT, STOP, STOP_LIMIT, TRAILING_STOP.
    /// 订单类型。
    /// </summary>
    public string OrderType { get; set; } = "MARKET";

    /// <summary>
    /// Order side: BUY, SELL, BUY_TO_COVER, SELL_SHORT.
    /// 订单方向。
    /// </summary>
    public string Side { get; set; } = "BUY";

    /// <summary>
    /// Order quantity.
    /// 订单数量。
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Limit price (required for LIMIT and STOP_LIMIT orders).
    /// 限价（LIMIT 和 STOP_LIMIT 订单必填）。
    /// </summary>
    public decimal? LimitPrice { get; set; }

    /// <summary>
    /// Stop price (required for STOP and STOP_LIMIT orders).
    /// 止损触发价（STOP 和 STOP_LIMIT 订单必填）。
    /// </summary>
    public decimal? StopPrice { get; set; }

    /// <summary>
    /// Time in force: DAY, GOOD_TILL_CANCEL, FILL_OR_KILL, IMMEDIATE_OR_CANCEL.
    /// 订单有效期。
    /// </summary>
    public string TimeInForce { get; set; } = "DAY";

    /// <summary>
    /// Asset type: EQUITY, OPTION, etc.
    /// 资产类型。
    /// </summary>
    public string AssetType { get; set; } = "EQUITY";
}