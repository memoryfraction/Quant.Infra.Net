namespace Quant.Infra.Net.Broker.CharlesSchwab;

/// <summary>
/// Schwab quote for a single symbol.
/// Schwab 单个标的报价。
/// </summary>
public class SchwabQuote
{
    /// <summary>
    /// Stock symbol.
    /// 股票代码。
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Bid price.
    /// 买一价。
    /// </summary>
    public decimal BidPrice { get; set; }

    /// <summary>
    /// Ask price.
    /// 卖一价。
    /// </summary>
    public decimal AskPrice { get; set; }

    /// <summary>
    /// Last traded price.
    /// 最新成交价。
    /// </summary>
    public decimal LastPrice { get; set; }

    /// <summary>
    /// High price of the day.
    /// 当日最高价。
    /// </summary>
    public decimal High { get; set; }

    /// <summary>
    /// Low price of the day.
    /// 当日最低价。
    /// </summary>
    public decimal Low { get; set; }

    /// <summary>
    /// Open price of the day.
    /// 当日开盘价。
    /// </summary>
    public decimal Open { get; set; }

    /// <summary>
    /// Previous close price.
    /// 前收盘价。
    /// </summary>
    public decimal Close { get; set; }

    /// <summary>
    /// Trading volume for the day.
    /// 当日成交量。
    /// </summary>
    public long Volume { get; set; }

    /// <summary>
    /// Net change from previous close.
    /// 较前收盘价的净变化。
    /// </summary>
    public decimal Change { get; set; }

    /// <summary>
    /// Percent change from previous close.
    /// 较前收盘价的百分比变化。
    /// </summary>
    public decimal ChangePercent { get; set; }

    /// <summary>
    /// Quote timestamp (Unix milliseconds).
    /// 报价时间戳（Unix 毫秒）。
    /// </summary>
    public long Timestamp { get; set; }
}