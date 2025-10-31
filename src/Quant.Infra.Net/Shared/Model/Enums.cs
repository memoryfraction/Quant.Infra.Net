using System.Runtime.Serialization;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// 交易所环境：测试、实盘
    /// </summary>
    public enum ExchangeEnvironment
    {
        Testnet=0,
        Live=1,
        Paper=2
    }

    public enum StartMode
    {
        NextSecond = 0,
        NextMinute = 1,
        NextHour = 2,
        NextDay = 3,
        TodayBeforeUSMarketClose = 4 // 通常指美东时间下午四点
    }


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
        Binance = 1,
        MongoDBWebApi = 2
    }

    public enum TradeDirection
    {
        Long = 0,  // 做多
        Short = 1  // 做空
    }

    public enum BrokerAccountType
    {
        Cash = 0,
        Margin = 1
    }

    public enum OrderActionType
    {
        Buy = 0,
        Sell = 1
    }


    public enum OrderExecutionType
    {
        /// <summary>
        /// Limit orders will be placed at a specific price. If the price isn't available in the order book for that asset, the order will be added in the order book for someone to fill.
        /// </summary>
        Limit = 0,

        /// <summary>
        /// Market order will be placed without a price. The order will be executed at the best price available at that time in the order book.
        /// </summary>
        Market = 1,

        /// <summary>
        /// Stop loss order. Will execute a market order when the price drops below a price to sell and therefore limit the loss.
        /// </summary>
        StopLoss = 2,

        /// <summary>
        /// Stop loss limit order. Will execute a limit order when the price drops below a price to sell, aiming to limit the loss but at a specific price.
        /// </summary>
        StopLossLimit = 3,

        /// <summary>
        /// Take profit order. Will execute a market order when the price rises above a certain point to secure profits.
        /// </summary>
        TakeProfit = 4,

        /// <summary>
        /// Take profit limit order. Will execute a limit order when the price rises above a certain point to secure profits at a specific price.
        /// </summary>
        TakeProfitLimit = 5,

        /// <summary>
        /// Trailing stop order. Follows the price movement by a certain distance and executes when the price reverses by that distance.
        /// </summary>
        TrailingStop = 6,

        /// <summary>
        /// Fills an order at a given price or better.
        /// </summary>
        FillOrKill = 7,

        /// <summary>
        /// Executes as much of the order as possible and cancels the remaining unfilled portion.
        /// </summary>
        ImmediateOrCancel = 8
    }


    public enum ContractSecurityType
    {
        /// <summary>
        /// 股票合约
        /// Stock contract
        /// </summary>
        Stock = 0,

        /// <summary>
        /// 期权合约
        /// Options contract
        /// </summary>
        Option = 1,

        /// <summary>
        /// 期货合约
        /// Futures contract
        /// </summary>
        Future = 2,

        /// <summary>
        /// 交易所交易基金
        /// Exchange Traded Fund
        /// </summary>
        ETF = 3,

        /// <summary>
        /// 其他合约类型
        /// Other contract types
        /// </summary>
        Other = 4
    }


    public enum Currency
    {
        USD=0,  // 美元
        CNY=1,  // 人民币
        HKD=2,  // 港币
        USDT=3, // 泰达币
        USDC=4  // USD Coi

        // 可以根据需要添加更多货币
    }


    public enum PairTradingActionType
    {
        Open=0, // 开仓
        TakeProfit=1,
        StopLoss=2,
        MeanReverseExit=3,
        DoNothing=4

        // 可以根据需要添加更多操作类型
    }

    public enum DownloadType
    {
        Spot,           // 现货
        PerpetualContract // 永续合约
    }

}