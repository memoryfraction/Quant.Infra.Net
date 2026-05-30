using Quant.Infra.Net.Portfolio.Models;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Interfaces
{
    /// <summary>
    /// Interface for Charles Schwab broker services.
    /// Charles Schwab 券商服务接口。
    /// </summary>
    public interface ISchwabBrokerService
    {
        /// <summary>
        /// Gets account information.
        /// 获取账户信息。
        /// </summary>
        Task<SchwabAccount> GetAccountAsync();

        /// <summary>
        /// Gets all positions.
        /// 获取所有持仓信息。
        /// </summary>
        Task<List<Position>> GetPositionsAsync();

        /// <summary>
        /// Gets a position for a specific symbol.
        /// 获取指定标的的持仓信息。
        /// </summary>
        Task<Position?> GetPositionAsync(string symbol);

        /// <summary>
        /// Gets a quote for a single symbol.
        /// 获取单个标的报价。
        /// </summary>
        Task<SchwabQuote> GetQuoteAsync(string symbol);

        /// <summary>
        /// Gets quotes for multiple symbols.
        /// 批量获取多个标的报价。
        /// </summary>
        Task<Dictionary<string, SchwabQuote>> GetQuotesAsync(List<string> symbols);

        /// <summary>
        /// Gets an option chain for a symbol.
        /// 获取指定标的的期权链。
        /// </summary>
        Task<SchwabOptionChain> GetOptionChainAsync(string symbol, string? contractType = null, int? strikeCount = null);

        /// <summary>
        /// Places an order.
        /// 提交订单。
        /// </summary>
        Task<string> PlaceOrderAsync(SchwabOrderRequest order);

        /// <summary>
        /// Gets an order by id.
        /// 根据订单编号获取订单。
        /// </summary>
        Task<SchwabOrder> GetOrderAsync(string orderId);

        /// <summary>
        /// Cancels an order by id.
        /// 根据订单编号取消订单。
        /// </summary>
        Task<bool> CancelOrderAsync(string orderId);

        /// <summary>
        /// Gets recent orders for the account.
        /// 获取账户最近订单。
        /// </summary>
        Task<List<SchwabOrder>> GetOrdersAsync(int maxResults = 100);

        /// <summary>
        /// Checks whether the market is open.
        /// 检查市场是否开盘。
        /// </summary>
        Task<bool> IsMarketOpenAsync();

        /// <summary>
        /// Gets price history (OHLCV candles) for a symbol.
        /// 获取指定标的的历史行情数据（OHLCV K 线）。
        /// </summary>
        /// <param name="symbol">The symbol (e.g., AAPL for equity, AAPL_20260515_185C00 for option).</param>
        /// <param name="startDate">Start date of the history range.</param>
        /// <param name="endDate">End date of the history range.</param>
        /// <param name="frequencyType">Frequency type: minute, daily, weekly, monthly.</param>
        /// <param name="frequency">Frequency multiplier (1, 5, 10, 15, 30 for minute; 1 for others).</param>
        /// <param name="needExtendedHoursData">Whether to include extended hours data.</param>
        /// <returns>Price history with candles array.</returns>
        Task<SchwabPriceHistory> GetPriceHistoryAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            string frequencyType,
            int frequency = 1,
            bool needExtendedHoursData = false);
    }

    #region Schwab Models

    /// <summary>
    /// Schwab account summary.
    /// Schwab 账户摘要。
    /// </summary>
    public class SchwabAccount
    {
        /// <summary>
        /// Account number.
        /// 账户号码。
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Account type.
        /// 账户类型。
        /// </summary>
        public string AccountType { get; set; } = string.Empty;

        /// <summary>
        /// Cash balance.
        /// 现金余额。
        /// </summary>
        public decimal CashBalance { get; set; }

        /// <summary>
        /// Long market value.
        /// 多头市值。
        /// </summary>
        public decimal MarketValue { get; set; }

        /// <summary>
        /// Total account equity.
        /// 账户总权益。
        /// </summary>
        public decimal TotalEquity { get; set; }

        /// <summary>
        /// Buying power.
        /// 购买力。
        /// </summary>
        public decimal BuyingPower { get; set; }

        /// <summary>
        /// Unrealized profit or loss.
        /// 未实现盈亏。
        /// </summary>
        public decimal UnrealizedPnL { get; set; }

        /// <summary>
        /// Realized profit or loss.
        /// 已实现盈亏。
        /// </summary>
        public decimal RealizedPnL { get; set; }
    }

    /// <summary>
    /// Schwab market quote.
    /// Schwab 市场报价。
    /// </summary>
    public class SchwabQuote
    {
        /// <summary>
        /// Symbol.
        /// 标的代码。
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Bid price.
        /// 买价。
        /// </summary>
        public decimal BidPrice { get; set; }

        /// <summary>
        /// Ask price.
        /// 卖价。
        /// </summary>
        public decimal AskPrice { get; set; }

        /// <summary>
        /// Last traded price.
        /// 最新成交价。
        /// </summary>
        public decimal LastPrice { get; set; }

        /// <summary>
        /// Volume.
        /// 成交量。
        /// </summary>
        public long Volume { get; set; }

        /// <summary>
        /// Session high price.
        /// 盘中最高价。
        /// </summary>
        public decimal High { get; set; }

        /// <summary>
        /// Session low price.
        /// 盘中最低价。
        /// </summary>
        public decimal Low { get; set; }

        /// <summary>
        /// Open price.
        /// 开盘价。
        /// </summary>
        public decimal Open { get; set; }

        /// <summary>
        /// Close price.
        /// 收盘价。
        /// </summary>
        public decimal Close { get; set; }

        /// <summary>
        /// Price change.
        /// 价格变动。
        /// </summary>
        public decimal Change { get; set; }

        /// <summary>
        /// Percent price change.
        /// 价格变动百分比。
        /// </summary>
        public decimal ChangePercent { get; set; }

        /// <summary>
        /// Quote timestamp.
        /// 报价时间戳。
        /// </summary>
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// Schwab option chain.
    /// Schwab 期权链。
    /// </summary>
    public class SchwabOptionChain
    {
        /// <summary>
        /// Underlying symbol.
        /// 标的代码。
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// API status.
        /// API 状态。
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Underlying price.
        /// 标的价格。
        /// </summary>
        public decimal UnderlyingPrice { get; set; }

        /// <summary>
        /// Call option contracts.
        /// 看涨期权合约。
        /// </summary>
        public List<SchwabOptionContract> CallOptions { get; set; } = new();

        /// <summary>
        /// Put option contracts.
        /// 看跌期权合约。
        /// </summary>
        public List<SchwabOptionContract> PutOptions { get; set; } = new();
    }

    /// <summary>
    /// Schwab option contract.
    /// Schwab 期权合约。
    /// </summary>
    public class SchwabOptionContract
    {
        /// <summary>
        /// Option symbol.
        /// 期权代码。
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Contract description.
        /// 合约描述。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Expiration date.
        /// 到期日期。
        /// </summary>
        public string ExpirationDate { get; set; } = string.Empty;

        /// <summary>
        /// Strike price.
        /// 行权价。
        /// </summary>
        public decimal Strike { get; set; }

        /// <summary>
        /// Contract type, such as CALL or PUT.
        /// 合约类型，例如 CALL 或 PUT。
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
        /// Mark price.
        /// 标记价格。
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
        /// Delta Greek.
        /// Delta 希腊值。
        /// </summary>
        public decimal Delta { get; set; }

        /// <summary>
        /// Gamma Greek.
        /// Gamma 希腊值。
        /// </summary>
        public decimal Gamma { get; set; }

        /// <summary>
        /// Theta Greek.
        /// Theta 希腊值。
        /// </summary>
        public decimal Theta { get; set; }

        /// <summary>
        /// Vega Greek.
        /// Vega 希腊值。
        /// </summary>
        public decimal Vega { get; set; }

        /// <summary>
        /// Rho Greek.
        /// Rho 希腊值。
        /// </summary>
        public decimal Rho { get; set; }

        /// <summary>
        /// Whether the contract is in the money.
        /// 合约是否为价内。
        /// </summary>
        public bool InTheMoney { get; set; }
    }

    /// <summary>
    /// Schwab order request.
    /// Schwab 订单请求。
    /// </summary>
    public class SchwabOrderRequest
    {
        /// <summary>
        /// Symbol to trade.
        /// 交易标的代码。
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Order type.
        /// 订单类型。
        /// </summary>
        public string OrderType { get; set; } = "MARKET";

        /// <summary>
        /// Order side or instruction.
        /// 订单方向或指令。
        /// </summary>
        public string Side { get; set; } = "BUY";

        /// <summary>
        /// Order quantity.
        /// 订单数量。
        /// </summary>
        public int Quantity { get; set; }

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
        /// Time in force.
        /// 订单有效期。
        /// </summary>
        public string TimeInForce { get; set; } = "DAY";

        /// <summary>
        /// Asset type.
        /// 资产类型。
        /// </summary>
        public string AssetType { get; set; } = "EQUITY";
    }

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
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Whether the result is empty (no candles returned).
        /// </summary>
        public bool Empty { get; set; }

        /// <summary>
        /// List of OHLCV candles.
        /// </summary>
        public List<SchwabPriceBar> Candles { get; set; } = new();

        /// <summary>
        /// Pagination metadata. Null when no pagination is applied.
        /// </summary>
        public PriceHistoryPagination? Pagination { get; set; }
    }

    /// <summary>
    /// Pagination metadata for price history responses.
    /// Follows the same maxResults style used by Schwab orders endpoint.
    /// </summary>
    public class PriceHistoryPagination
    {
        /// <summary>
        /// Total number of candles across all pages.
        /// </summary>
        public int TotalCandles { get; set; }

        /// <summary>
        /// Maximum number of candles returned per request.
        /// </summary>
        public int MaxResults { get; set; }

        /// <summary>
        /// Zero-based offset of the first candle in this page.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>
        /// Whether more pages are available after this one.
        /// </summary>
        public bool HasMore { get; set; }
    }

    #endregion
}
