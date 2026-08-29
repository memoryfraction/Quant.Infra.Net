namespace Quant.Infra.Net.Broker.CharlesSchwab;

/// <summary>
/// Single OHLCV price bar (candle) returned by price history API.
/// 单根 OHLCV K 线。
/// </summary>
public class SchwabPriceBar
{
    /// <summary>
    /// Open price.
    /// 开盘价。
    /// </summary>
    public decimal Open { get; set; }

    /// <summary>
    /// High price.
    /// 最高价。
    /// </summary>
    public decimal High { get; set; }

    /// <summary>
    /// Low price.
    /// 最低价。
    /// </summary>
    public decimal Low { get; set; }

    /// <summary>
    /// Close price.
    /// 收盘价。
    /// </summary>
    public decimal Close { get; set; }

    /// <summary>
    /// Volume.
    /// 成交量。
    /// </summary>
    public long Volume { get; set; }

    /// <summary>
    /// Candle timestamp (UTC).
    /// K 线时间戳（UTC）。
    /// </summary>
    public DateTime Datetime { get; set; }
}

/// <summary>
/// Schwab price history response containing a list of OHLCV candles.
/// Schwab 历史行情响应，包含 OHLCV K 线列表。
/// </summary>
public class SchwabPriceHistory
{
    /// <summary>
    /// The symbol queried.
    /// 查询的标的代码。
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Whether the result is empty (no candles returned).
    /// 结果是否为空（无 K 线返回）。
    /// </summary>
    public bool Empty { get; set; }

    /// <summary>
    /// List of OHLCV candles.
    /// OHLCV K 线列表。
    /// </summary>
    public List<SchwabPriceBar> Candles { get; set; } = new();

    /// <summary>
    /// Pagination metadata. Null when no pagination is applied.
    /// 分页元数据。未分页时为 null。
    /// </summary>
    public PriceHistoryPagination? Pagination { get; set; }
}

/// <summary>
/// Pagination metadata for price history responses.
/// 历史行情分页元数据。
/// </summary>
public class PriceHistoryPagination
{
    /// <summary>
    /// Total number of candles across all pages.
    /// 所有页的总 K 线数量。
    /// </summary>
    public int TotalCandles { get; set; }

    /// <summary>
    /// Maximum number of candles returned per request.
    /// 每页最大 K 线数量。
    /// </summary>
    public int MaxResults { get; set; }

    /// <summary>
    /// Zero-based offset of the first candle in this page.
    /// 当前页起始偏移（从 0 开始）。
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// Whether more pages are available after this one.
    /// 是否有更多页。
    /// </summary>
    public bool HasMore { get; set; }
}