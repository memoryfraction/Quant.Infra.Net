using System.Runtime.Serialization;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// 代表不同市场类型
    /// Represents different market types
    /// </summary>
    public enum MarketType
    {
        /// <summary>
        /// 加密货币市场，24小时交易
        /// Cryptocurrency market, open 24 hours
        /// </summary>
        Crypto,

        /// <summary>
        /// 美股市场，早9:30到16:00交易
        /// US equity market, open from 9:30 AM to 4:00 PM
        /// </summary>
        UsEquityMarket,

        /// <summary>
        /// 香港股市
        /// Hong Kong equity market
        /// </summary>
        HkEquityMarket,

        /// <summary>
        /// 中国股市
        /// China equity market
        /// </summary>
        ChinaEquityMarket,

        /// <summary>
        /// 外汇市场
        /// Forex market
        /// </summary>
        Forex,

        /// <summary>
        /// 大宗商品市场
        /// Commodities market
        /// </summary>
        Commodities,

        /// <summary>
        /// 欧洲股市
        /// European equity market
        /// </summary>
        EuropeEquityMarket,

        /// <summary>
        /// 印度股市
        /// Indian equity market
        /// </summary>
        IndiaEquityMarket
    }

    public enum Broker
    {
        Binance,              // Binance
        OKEX,                 // OKEX
        InteractiveBrokers,   // Interactive Brokers
        Coinbase,             // Coinbase
        Kraken,               // Kraken
        Bitfinex,             // Bitfinex
        Bitstamp,             // Bitstamp
        FTX,                  // FTX (assuming still supported)
        Deribit,              // Deribit
        Huobi,                // Huobi
        Kucoin,               // Kucoin
        Gemini                // Gemini
    }

    /// <summary>
    /// 表示订单的不同状态。
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>
        /// 新订单，已创建但尚未处理。
        /// </summary>
        New,

        /// <summary>
        /// 部分成交，订单的一部分数量已经成交。
        /// </summary>
        PartiallyFilled,

        /// <summary>
        /// 全部成交，订单的所有数量已经成交。
        /// </summary>
        Filled,

        /// <summary>
        /// 待处理，订单已提交但尚未被券商确认或接受。
        /// </summary>
        PendingNew,

        /// <summary>
        /// 已拒绝，订单因某些原因被券商拒绝，未能进入市场或成交。
        /// </summary>
        Rejected,

        /// <summary>
        /// 已取消，订单在未成交的情况下被用户或系统取消。
        /// </summary>
        Cancelled,

        /// <summary>
        /// 取消中，取消请求已提交，但尚未确认取消。
        /// </summary>
        PendingCancel,

        /// <summary>
        /// 已过期，订单因超过有效期未能成交而失效。
        /// </summary>
        Expired,

        /// <summary>
        /// 已暂停，订单被暂停执行。
        /// </summary>
        Suspended,

        /// <summary>
        /// 修改中，修改请求已提交，但尚未确认修改。
        /// </summary>
        PendingReplace
    }

    public enum AssetType
    {
        UsEquity = 1,                  // US Equity
        UsOption = 2,                  // US Option
        CryptoSpot = 3,                // Cryptocurrency Spot
        CryptoPerpetualContract = 4,   // Cryptocurrency Perpetual Contract
        CnEquity = 5,                  // China Equity
        HkEquity = 6,                  // Hong Kong Equity
        CryptoOption = 7               // Cryptocurrency Option
    }

    public enum ResolutionLevel
    {
        [EnumMember(Value = "t")]
        Tick,

        [EnumMember(Value = "s")]
        Second,

        [EnumMember(Value = "min")]
        Minute,

        [EnumMember(Value = "h")]
        Hourly,

        [EnumMember(Value = "d")]
        Daily,

        [EnumMember(Value = "wk")]
        Weekly,

        [EnumMember(Value = "mo")]
        Monthly,

        [EnumMember(Value = "other")]
        Other
    }

    public enum DataSource
    {
        YahooFinance,
        Binance
    }

    public enum TradeDirection
    {
        Long,  // 做多
        Short  // 做空
    }

    public enum OrderActionType
    {
        Buy,
        Sell
    }

    public enum OrderExecutionType
    {
        /// <summary>
        /// Limit orders will be placed at a specific price. If the price isn't available in the order book for that asset the order will be added in the order book for someone to fill.
        /// </summary>
        Limit,

        /// <summary>
        /// Market order will be placed without a price. The order will be executed at the best price available at that time in the order book.
        /// </summary>
        Market,

        /// <summary>
        /// Stop loss order. Will execute a market order when the price drops below a price to sell and therefor limit the loss
        /// </summary>
        StopLoss,

        /// <summary>
        /// Stop loss order. Will execute a limit order when the price drops below a price to sell and therefor limit the loss
        /// </summary>
        StopLossLimit,

        /// <summary>
        /// Take profit order. Will execute a market order when the price rises above a price to sell and therefor take a profit
        /// </summary>
        TakeProfit,

        /// <summary>
        /// Take profit limit order. Will execute a limit order when the price rises above a price to sell and therefor take a profit
        /// </summary>
        TakeProfitLimit,

        /// <summary>
        /// Same as a limit order, however it will fail if the order would immediately match, therefor preventing taker orders
        /// </summary>
        LimitMaker
    }

    public enum ContractSecurityType
    {
        Stock
    }

    public enum Currency
    {
        USD,  // 美元
        CNY,  // 人民币
        HKD,  // 港币
        USDT, // 泰达币
        USDC  // USD Coin
    }
}