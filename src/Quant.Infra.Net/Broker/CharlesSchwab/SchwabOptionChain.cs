namespace Quant.Infra.Net.Broker.CharlesSchwab;

/// <summary>
/// Schwab option chain response.
/// Schwab 期权链响应。
/// </summary>
public class SchwabOptionChain
{
    /// <summary>
    /// Underlying symbol.
    /// 标的代码。
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Status of the option chain (e.g., REALTIME).
    /// 期权链状态。
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Current price of the underlying.
    /// 标的当前价格。
    /// </summary>
    public decimal UnderlyingPrice { get; set; }

    /// <summary>
    /// List of call option contracts.
    /// 看涨期权列表。
    /// </summary>
    public List<SchwabOptionContract> CallOptions { get; set; } = new();

    /// <summary>
    /// List of put option contracts.
    /// 看跌期权列表。
    /// </summary>
    public List<SchwabOptionContract> PutOptions { get; set; } = new();

    /// <summary>
    /// Total number of option contracts (calls + puts).
    /// 期权合约总数（看涨 + 看跌）。
    /// </summary>
    public int TotalContracts => CallOptions.Count + PutOptions.Count;
}

/// <summary>
/// Schwab option contract details.
/// Schwab 期权合约详情。
/// </summary>
public class SchwabOptionContract
{
    /// <summary>
    /// Option symbol (e.g., AAPL_20260515_185C00).
    /// 期权代码。
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Description of the option contract.
    /// 期权合约描述。
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Expiration date string (e.g., "2026-05-15").
    /// 到期日期字符串。
    /// </summary>
    public string ExpirationDate { get; set; } = string.Empty;

    /// <summary>
    /// Strike price.
    /// 行权价。
    /// </summary>
    public decimal Strike { get; set; }

    /// <summary>
    /// Option type: CALL or PUT.
    /// 期权类型：CALL 或 PUT。
    /// </summary>
    public string ContractType { get; set; } = string.Empty;

    /// <summary>
    /// Bid price.
    /// 买价。
    /// </summary>
    public decimal Bid { get; set; }

    /// <summary>
    /// Ask price.
    /// 卖价。
    /// </summary>
    public decimal Ask { get; set; }

    /// <summary>
    /// Last traded price.
    /// 最新成交价。
    /// </summary>
    public decimal Last { get; set; }

    /// <summary>
    /// Mark price (midpoint of bid/ask).
    /// 标记价格（买卖中间价）。
    /// </summary>
    public decimal Mark { get; set; }

    /// <summary>
    /// Volume.
    /// 成交量。
    /// </summary>
    public long Volume { get; set; }

    /// <summary>
    /// Open interest.
    /// 未平仓量。
    /// </summary>
    public long OpenInterest { get; set; }

    /// <summary>
    /// Implied volatility.
    /// 隐含波动率。
    /// </summary>
    public decimal ImpliedVolatility { get; set; }

    /// <summary>
    /// Delta.
    /// Delta。
    /// </summary>
    public decimal Delta { get; set; }

    /// <summary>
    /// Gamma.
    /// Gamma。
    /// </summary>
    public decimal Gamma { get; set; }

    /// <summary>
    /// Theta.
    /// Theta。
    /// </summary>
    public decimal Theta { get; set; }

    /// <summary>
    /// Vega.
    /// Vega。
    /// </summary>
    public decimal Vega { get; set; }

    /// <summary>
    /// Rho.
    /// Rho。
    /// </summary>
    public decimal Rho { get; set; }

    /// <summary>
    /// Whether the option is in the money.
    /// 是否为实值期权。
    /// </summary>
    public bool InTheMoney { get; set; }
}