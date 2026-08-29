namespace Quant.Infra.Net.Broker.CharlesSchwab;

/// <summary>
/// Schwab order.
/// Schwab 订单。
/// </summary>
public class SchwabOrder
{
    /// <summary>
    /// Order id.
    /// 订单编号。
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Order symbol.
    /// 订单标的。
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Order status.
    /// 订单状态。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Order type.
    /// 订单类型。
    /// </summary>
    public string OrderType { get; set; } = string.Empty;

    /// <summary>
    /// Order side or instruction.
    /// 订单方向或指令。
    /// </summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>
    /// Order quantity.
    /// 订单数量。
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Filled quantity.
    /// 已成交数量。
    /// </summary>
    public int FilledQuantity { get; set; }

    /// <summary>
    /// Limit price.
    /// 限价。
    /// </summary>
    public decimal? LimitPrice { get; set; }

    /// <summary>
    /// Stop price.
    /// 止损触发价。
    /// </summary>
    public decimal? StopPrice { get; set; }

    /// <summary>
    /// Average filled price.
    /// 平均成交价。
    /// </summary>
    public decimal? AverageFilledPrice { get; set; }

    /// <summary>
    /// Time in force.
    /// 订单有效期。
    /// </summary>
    public string TimeInForce { get; set; } = string.Empty;

    /// <summary>
    /// Created timestamp.
    /// 创建时间。
    /// </summary>
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>
    /// Updated timestamp.
    /// 更新时间。
    /// </summary>
    public string UpdatedAt { get; set; } = string.Empty;
}