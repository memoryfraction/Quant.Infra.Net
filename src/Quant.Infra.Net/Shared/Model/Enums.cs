using System.Runtime.Serialization;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// 交易所环境：测试网、实盘、模拟盘。
    /// Exchange environment: testnet, live, or paper trading.
    /// </summary>
    public enum ExchangeEnvironment
    {
        /// <summary>
        /// 测试网环境，用于开发和测试 / Testnet environment for development and testing.
        /// </summary>
        Testnet = 0,
        
        /// <summary>
        /// 实盘环境，使用真实资金进行交易 / Live environment with real funds for trading.
        /// </summary>
        Live = 1,
        
        /// <summary>
        /// 模拟盘环境，使用虚拟资金模拟实盘 / Paper trading environment with virtual funds simulating live trading.
        /// </summary>
        Paper = 2
    }

    /// <summary>
    /// 启动模式：定义定时任务的触发时机。
    /// Start mode: defines the trigger timing for scheduled tasks.
    /// </summary>
    public enum StartMode
    {
        /// <summary>
        /// 下一秒触发 / Trigger at the next second.
        /// </summary>
        NextSecond = 0,
        /// <summary>
        /// 下一分钟触发 / Trigger at the next minute.
        /// </summary>
        NextMinute = 1,
        /// <summary>
        /// 下一小时触发 / Trigger at the next hour.
        /// </summary>
        NextHour = 2,
        /// <summary>
        /// 下一天触发 / Trigger at the next day.
        /// </summary>
        NextDay = 3,
        /// <summary>
        /// 美股收盘前触发（通常指美东时间下午16:00）
        /// Trigger before US market close (typically 4:00 PM Eastern Time).
        /// </summary>
        TodayBeforeUSMarketClose = 4
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

    /// <summary>
    /// 经纪商枚举：定义支持的交易经纪商。
    /// Broker enum: defines supported trading brokers.
    /// </summary>
    public enum Broker
    {
        /// <summary>
        /// 币安交易所 / Binance exchange.
        /// </summary>
        Binance = 1,
        
        /// <summary>
        /// OKEX交易所 / OKEX exchange.
        /// </summary>
        OKEX = 2,
        
        /// <summary>
        /// 盈透证券 / Interactive Brokers.
        /// </summary>
        InteractiveBrokers = 3,
        
        /// <summary>
        /// Coinbase交易所 / Coinbase exchange.
        /// </summary>
        Coinbase = 4,
        
        /// <summary>
        /// Kraken交易所 / Kraken exchange.
        /// </summary>
        Kraken = 5,
        
        /// <summary>
        /// Bitfinex交易所 / Bitfinex exchange.
        /// </summary>
        Bitfinex = 6,
        
        /// <summary>
        /// Bitstamp交易所 / Bitstamp exchange.
        /// </summary>
        Bitstamp = 7,
        
        /// <summary>
        /// FTX交易所（已破产）/ FTX exchange (bankrupted).
        /// </summary>
        FTX = 8,
        
        /// <summary>
        /// Deribit交易所 / Deribit exchange.
        /// </summary>
        Deribit = 9,
        
        /// <summary>
        /// 火币交易所 / Huobi exchange.
        /// </summary>
        Huobi = 10,
        
        /// <summary>
        /// Kucoin交易所 / Kucoin exchange.
        /// </summary>
        Kucoin = 11,
        
        /// <summary>
        /// Gemini交易所 / Gemini exchange.
        /// </summary>
        Gemini = 12
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


    /// <summary>
    /// 资产类型枚举：定义不同的资产类型。
    /// Asset type enum: defines different asset types.
    /// </summary>
    public enum AssetType
    {
        /// <summary>
        /// 其他类型 / Other type.
        /// </summary>
        Other = 0,
        
        /// <summary>
        /// 美股 / US Equity.
        /// </summary>
        UsEquity = 1,
        
        /// <summary>
        /// 美股期权 / US Option.
        /// </summary>
        UsOption = 2,
        
        /// <summary>
        /// 加密货币现货 / Cryptocurrency Spot.
        /// </summary>
        CryptoSpot = 3,
        
        /// <summary>
        /// 加密货币永续合约 / Cryptocurrency Perpetual Contract.
        /// </summary>
        CryptoPerpetualContract = 4,
        
        /// <summary>
        /// A股 / China Equity.
        /// </summary>
        CnEquity = 5,
        
        /// <summary>
        /// 港股 / Hong Kong Equity.
        /// </summary>
        HkEquity = 6,
        
        /// <summary>
        /// 加密货币期权 / Cryptocurrency Option.
        /// </summary>
        CryptoOption = 7
    }

    /// <summary>
    /// 分辨率级别：定义时间序列的分辨率。
    /// Resolution level: defines the resolution of time series data.
    /// </summary>
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

    /// <summary>
    /// 数据源枚举：定义支持的数据源。
    /// Data source enum: defines supported data sources.
    /// </summary>
    public enum DataSource
    {
        /// <summary>
        /// Yahoo Finance数据源 / Yahoo Finance data source.
        /// </summary>
        YahooFinance = 0,
        
        /// <summary>
        /// Binance数据源 / Binance data source.
        /// </summary>
        Binance = 1,
        
        /// <summary>
        /// MongoDB Web API数据源 / MongoDB Web API data source.
        /// </summary>
        MongoDBWebApi = 2
    }

    /// <summary>
    /// 交易方向：做多或做空。
    /// Trade direction: long or short.
    /// </summary>
    public enum TradeDirection
    {
        /// <summary>
        /// 做多 / Long position.
        /// </summary>
        Long = 0,
        /// <summary>
        /// 做空 / Short position.
        /// </summary>
        Short = 1
    }

    /// <summary>
    /// 经纪账户类型：现金账户或保证金账户。
    /// Broker account type: cash or margin account.
    /// </summary>
    public enum BrokerAccountType
    {
        Cash = 0,
        Margin = 1
    }

    /// <summary>
    /// 订单操作类型：买入或卖出。
    /// Order action type: buy or sell.
    /// </summary>
    public enum OrderActionType
    {
        Buy = 0,
        Sell = 1
    }


    /// <summary>
    /// 订单执行类型：定义不同的订单执行方式。
    /// Order execution type: defines different order execution methods.
    /// </summary>
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


    /// <summary>
    /// 货币类型枚举。
    /// Currency type enum.
    /// </summary>
    public enum Currency
    {
        /// <summary>
        /// 美元 / US Dollar.
        /// </summary>
        USD = 0,
        
        /// <summary>
        /// 人民币 / Chinese Yuan.
        /// </summary>
        CNY = 1,
        
        /// <summary>
        /// 港币 / Hong Kong Dollar.
        /// </summary>
        HKD = 2,
        
        /// <summary>
        /// 泰达币 / Tether (USDT).
        /// </summary>
        USDT = 3,
        
        /// <summary>
        /// USD Coin / USD Coin (USDC).
        /// </summary>
        USDC = 4
    }


    /// <summary>
    /// 配对交易操作类型。
    /// Pair trading action type.
    /// </summary>
    public enum PairTradingActionType
    {
        /// <summary>
        /// 开仓 / Open position.
        /// </summary>
        Open = 0,
        
        /// <summary>
        /// 止盈 / Take profit.
        /// </summary>
        TakeProfit = 1,
        
        /// <summary>
        /// 止损 / Stop loss.
        /// </summary>
        StopLoss = 2,
        
        /// <summary>
        /// 均值回归退出 / Mean reversion exit.
        /// </summary>
        MeanReverseExit = 3,
        
        /// <summary>
        /// 不操作 / Do nothing.
        /// </summary>
        DoNothing = 4
    }

    /// <summary>
    /// 下载类型：现货或永续合约。
    /// Download type: spot or perpetual contract.
    /// </summary>
    public enum DownloadType
    {
        /// <summary>
        /// 现货 / Spot.
        /// </summary>
        Spot,
        /// <summary>
        /// 永续合约 / Perpetual contract.
        /// </summary>
        PerpetualContract
    }

}