using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财期权链服务接口。
    /// Charles Schwab option chain service interface.
    /// </summary>
    public interface ISchwabOptionChainService
    {
        /// <summary>
        /// 获取期权链数据。
        /// Gets option chain data.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="symbol">标的股票代码。 / Underlying symbol.</param>
        /// <param name="contractType">合约类型（CALL, PUT, ALL）。 / Contract type (CALL, PUT, ALL).</param>
        /// <param name="strikeCount">行权价数量。 / Strike count.</param>
        /// <param name="includeUnderlyingQuote">是否包含标的报价。 / Whether to include underlying quote.</param>
        /// <param name="strategy">策略类型（SINGLE, ANALYTICAL, COVERED, VERTICAL, etc.）。 / Strategy type.</param>
        /// <param name="range">价格范围（ITM, NTM, OTM, SAK, SBK, SNK, ALL）。 / Price range.</param>
        /// <param name="fromDate">起始日期（可选）。 / From date (optional).</param>
        /// <param name="toDate">结束日期（可选）。 / To date (optional).</param>
        /// <returns>期权链数据。 / Option chain data.</returns>
        Task<SchwabOptionChain> GetOptionChainAsync(
            Guid userId,
            string symbol,
            string contractType = "ALL",
            int strikeCount = 10,
            bool includeUnderlyingQuote = true,
            string strategy = "SINGLE",
            string range = "ALL",
            DateTimeOffset? fromDate = null,
            DateTimeOffset? toDate = null);

        /// <summary>
        /// 获取期权到期日列表。
        /// Gets option expiration dates.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="symbol">标的股票代码。 / Underlying symbol.</param>
        /// <returns>到期日列表。 / List of expiration dates.</returns>
        Task<IReadOnlyList<DateTimeOffset>> GetExpirationDatesAsync(Guid userId, string symbol);
    }

    /// <summary>
    /// 嘉信理财期权链数据。
    /// Charles Schwab option chain data.
    /// </summary>
    public class SchwabOptionChain
    {
        /// <summary>
        /// 标的股票代码。
        /// Underlying symbol.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 标的股票报价。
        /// Underlying quote.
        /// </summary>
        public SchwabQuote? UnderlyingQuote { get; set; }

        /// <summary>
        /// 策略类型。
        /// Strategy type.
        /// </summary>
        public string Strategy { get; set; } = string.Empty;

        /// <summary>
        /// 间隔（行权价间隔）。
        /// Interval (strike price interval).
        /// </summary>
        public decimal Interval { get; set; }

        /// <summary>
        /// 是否延迟数据。
        /// Whether data is delayed.
        /// </summary>
        public bool IsDelayed { get; set; }

        /// <summary>
        /// 是否指数期权。
        /// Whether index option.
        /// </summary>
        public bool IsIndex { get; set; }

        /// <summary>
        /// 到期日期映射（日期 -> 期权合约列表）。
        /// Expiration date map (date -> option contracts list).
        /// </summary>
        public Dictionary<DateTimeOffset, SchwabOptionExpirationDate> CallExpDateMap { get; set; } = new();

        /// <summary>
        /// 认沽期权到期日期映射。
        /// Put option expiration date map.
        /// </summary>
        public Dictionary<DateTimeOffset, SchwabOptionExpirationDate> PutExpDateMap { get; set; } = new();

        /// <summary>
        /// 月度策略列表。
        /// Monthly strategy list.
        /// </summary>
        public List<string> MonthlyStrategyList { get; set; } = new();
    }

    /// <summary>
    /// 嘉信理财期权到期日数据。
    /// Charles Schwab option expiration date data.
    /// </summary>
    public class SchwabOptionExpirationDate
    {
        /// <summary>
        /// 到期日期。
        /// Expiration date.
        /// </summary>
        public DateTimeOffset ExpirationDate { get; set; }

        /// <summary>
        /// 距离到期天数。
        /// Days to expiration.
        /// </summary>
        public int DaysToExpiration { get; set; }

        /// <summary>
        /// 到期类型（S=标准, W=周, Q=季度）。
        /// Expiration type (S=Standard, W=Weekly, Q=Quarterly).
        /// </summary>
        public string ExpirationType { get; set; } = string.Empty;

        /// <summary>
        /// 结算类型（PM=下午, AM=上午）。
        /// Settlement type (PM=Afternoon, AM=Morning).
        /// </summary>
        public string SettlementType { get; set; } = string.Empty;

        /// <summary>
        /// 期权合约映射（行权价 -> 期权合约）。
        /// Option contract map (strike price -> option contract).
        /// </summary>
        public Dictionary<decimal, SchwabOptionContract> OptionContracts { get; set; } = new();
    }

    /// <summary>
    /// 嘉信理财期权合约。
    /// Charles Schwab option contract.
    /// </summary>
    public class SchwabOptionContract
    {
        /// <summary>
        /// 期权代码（OCC 格式）。
        /// Option symbol (OCC format).
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 期权描述。
        /// Option description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 交易所代码。
        /// Exchange code.
        /// </summary>
        public string ExchangeName { get; set; } = string.Empty;

        /// <summary>
        /// 买价。
        /// Bid price.
        /// </summary>
        public decimal Bid { get; set; }

        /// <summary>
        /// 卖价。
        /// Ask price.
        /// </summary>
        public decimal Ask { get; set; }

        /// <summary>
        /// 最新价。
        /// Last price.
        /// </summary>
        public decimal Last { get; set; }

        /// <summary>
        /// 标记价格。
        /// Mark price.
        /// </summary>
        public decimal Mark { get; set; }

        /// <summary>
        /// 买量。
        /// Bid size.
        /// </summary>
        public int BidSize { get; set; }

        /// <summary>
        /// 卖量。
        /// Ask size.
        /// </summary>
        public int AskSize { get; set; }

        /// <summary>
        /// 买卖价差。
        /// Bid-ask spread.
        /// </summary>
        public decimal BidAskSize { get; set; }

        /// <summary>
        /// 最后交易量。
        /// Last size.
        /// </summary>
        public int LastSize { get; set; }

        /// <summary>
        /// 最高价。
        /// High price.
        /// </summary>
        public decimal HighPrice { get; set; }

        /// <summary>
        /// 最低价。
        /// Low price.
        /// </summary>
        public decimal LowPrice { get; set; }

        /// <summary>
        /// 开盘价。
        /// Open price.
        /// </summary>
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// 收盘价。
        /// Close price.
        /// </summary>
        public decimal ClosePrice { get; set; }

        /// <summary>
        /// 成交量。
        /// Total volume.
        /// </summary>
        public long TotalVolume { get; set; }

        /// <summary>
        /// 交易时间。
        /// Trade time.
        /// </summary>
        public DateTimeOffset TradeTimeInLong { get; set; }

        /// <summary>
        /// 报价时间。
        /// Quote time.
        /// </summary>
        public DateTimeOffset QuoteTimeInLong { get; set; }

        /// <summary>
        /// 涨跌额。
        /// Net change.
        /// </summary>
        public decimal NetChange { get; set; }

        /// <summary>
        /// 波动率。
        /// Volatility.
        /// </summary>
        public decimal Volatility { get; set; }

        /// <summary>
        /// Delta 值。
        /// Delta value.
        /// </summary>
        public decimal Delta { get; set; }

        /// <summary>
        /// Gamma 值。
        /// Gamma value.
        /// </summary>
        public decimal Gamma { get; set; }

        /// <summary>
        /// Theta 值。
        /// Theta value.
        /// </summary>
        public decimal Theta { get; set; }

        /// <summary>
        /// Vega 值。
        /// Vega value.
        /// </summary>
        public decimal Vega { get; set; }

        /// <summary>
        /// Rho 值。
        /// Rho value.
        /// </summary>
        public decimal Rho { get; set; }

        /// <summary>
        /// 未平仓合约数。
        /// Open interest.
        /// </summary>
        public int OpenInterest { get; set; }

        /// <summary>
        /// 时间价值。
        /// Time value.
        /// </summary>
        public decimal TimeValue { get; set; }

        /// <summary>
        /// 理论期权价值。
        /// Theoretical option value.
        /// </summary>
        public decimal TheoreticalOptionValue { get; set; }

        /// <summary>
        /// 理论波动率。
        /// Theoretical volatility.
        /// </summary>
        public decimal TheoreticalVolatility { get; set; }

        /// <summary>
        /// 行权价。
        /// Strike price.
        /// </summary>
        public decimal StrikePrice { get; set; }

        /// <summary>
        /// 到期日期。
        /// Expiration date.
        /// </summary>
        public DateTimeOffset ExpirationDate { get; set; }

        /// <summary>
        /// 距离到期天数。
        /// Days to expiration.
        /// </summary>
        public int DaysToExpiration { get; set; }

        /// <summary>
        /// 到期类型。
        /// Expiration type.
        /// </summary>
        public string ExpirationType { get; set; } = string.Empty;

        /// <summary>
        /// 最后交易日期。
        /// Last trading day.
        /// </summary>
        public DateTimeOffset LastTradingDay { get; set; }

        /// <summary>
        /// 乘数（通常为 100）。
        /// Multiplier (usually 100).
        /// </summary>
        public int Multiplier { get; set; } = 100;

        /// <summary>
        /// 结算类型。
        /// Settlement type.
        /// </summary>
        public string SettlementType { get; set; } = string.Empty;

        /// <summary>
        /// 可交割说明。
        /// Deliverable note.
        /// </summary>
        public string DeliverableNote { get; set; } = string.Empty;

        /// <summary>
        /// 是否价内期权。
        /// Whether in the money.
        /// </summary>
        public bool InTheMoney { get; set; }

        /// <summary>
        /// 期权类型（CALL, PUT）。
        /// Option type (CALL, PUT).
        /// </summary>
        public string OptionType { get; set; } = string.Empty;

        /// <summary>
        /// 是否迷你期权。
        /// Whether mini option.
        /// </summary>
        public bool Mini { get; set; }

        /// <summary>
        /// 是否非标准期权。
        /// Whether non-standard option.
        /// </summary>
        public bool NonStandard { get; set; }

        /// <summary>
        /// 内在价值。
        /// Intrinsic value.
        /// </summary>
        public decimal IntrinsicValue { get; set; }

        /// <summary>
        /// 外在价值。
        /// Extrinsic value.
        /// </summary>
        public decimal ExtrinsicValue { get; set; }

        /// <summary>
        /// 期权可交割列表。
        /// Option deliverables list.
        /// </summary>
        public List<SchwabOptionDeliverable> OptionDeliverablesList { get; set; } = new();
    }

    /// <summary>
    /// 嘉信理财期权可交割物。
    /// Charles Schwab option deliverable.
    /// </summary>
    public class SchwabOptionDeliverable
    {
        /// <summary>
        /// 标的股票代码。
        /// Underlying symbol.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 资产类型。
        /// Asset type.
        /// </summary>
        public string AssetType { get; set; } = string.Empty;

        /// <summary>
        /// 可交割单位数量。
        /// Deliverable unit quantity.
        /// </summary>
        public decimal DeliverableUnits { get; set; }

        /// <summary>
        /// 货币类型。
        /// Currency type.
        /// </summary>
        public string CurrencyType { get; set; } = "USD";
    }
}
