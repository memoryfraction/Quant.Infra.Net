using System.Runtime.Serialization;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// 代表不同市场类型
    /// Represents different market types
    /// </summary>
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
        Crypto = 1,

        /// <summary>
        /// 美股市场，早9:30到16:00交易
        /// US equity market, open from 9:30 AM to 4:00 PM
        /// </summary>
        UsEquityMarket = 2,

        /// <summary>
        /// 香港股市
        /// Hong Kong equity market
        /// </summary>
        HkEquityMarket = 3,

        /// <summary>
        /// 欧洲股市
        /// European equity market
        /// </summary>
        EuEquityMarket = 4
    }

    public enum Broker
    {
        Binance = 1,              // Binance
        OKEX = 2,                 // OKEX
        InteractiveBrokers = 3,   // Interactive Brokers
        Coinbase = 4,             // Coinbase
        Kraken = 5,               // Kraken
        Bitfinex = 6,             // Bitfinex
        Bitstamp = 7,             // Bitstamp
        FTX = 8,                  // FTX
        Deribit = 9,              // Deribit
        Huobi = 10,               // Huobi
        Kucoin = 11,              // Kucoin
        Gemini = 12               // Gemini
    }


    /// <summary>
    /// 表示订单的不同状态。
    /// Represents different order statuses.
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>
        /// 新订单，已创建但尚未处理。
        /// New order, created but not yet processed.
        /// </summary>
        New = 1,

        /// <summary>
        /// 部分成交，订单的一部分数量已经成交。
        /// Partially filled, part of the order quantity has been filled.
        /// </summary>
        PartiallyFilled = 2,

        /// <summary>
        /// 全部成交，订单的所有数量已经成交。
        /// Fully filled, the entire order quantity has been filled.
        /// </summary>
        Filled = 3,

        /// <summary>
        /// 待处理，订单已提交但尚未被券商确认或接受。
        /// Pending, the order has been submitted but not yet confirmed or accepted by the broker.
        /// </summary>
        PendingNew = 4,

        /// <summary>
        /// 已拒绝，订单因某些原因被拒绝。
        /// Rejected, the order was rejected for some reason.
        /// </summary>
        Rejected = 5,

        /// <summary>
        /// 已取消，订单在执行之前被取消。
        /// Cancelled, the order was cancelled before execution.
        /// </summary>
        Canceled = 6,

        /// <summary>
        /// 已过期，订单因时效问题而过期，未能执行。
        /// Expired, the order expired and was not executed.
        /// </summary>
        Expired = 7
    }


    public enum AssetType
    {
        Other = 0,
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
        Tick = 0,

        [EnumMember(Value = "s")]
        Second = 1,

        [EnumMember(Value = "min")]
        Minute = 2,

        [EnumMember(Value = "h")]
        Hourly = 3,

        [EnumMember(Value = "d")]
        Daily = 4,

        [EnumMember(Value = "wk")]
        Weekly = 5,

        [EnumMember(Value = "mo")]
        Monthly = 6,

        [EnumMember(Value = "other")]
        Other = 7
    }

    public enum DataSource
    {
        YahooFinance = 0,
        Binance = 1
    }

    public enum TradeDirection
    {
        Long = 0,  // 做多
        Short = 1  // 做空
    }

    public enum OrderActionType
    {
        Buy = 0,
        Sell = 1
    }


}