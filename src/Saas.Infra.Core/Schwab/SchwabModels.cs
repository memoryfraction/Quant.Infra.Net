using System;
using System.Collections.Generic;

namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财账户号码。
    /// Charles Schwab account number.
    /// </summary>
    public class SchwabAccountNumber
    {
        /// <summary>
        /// 账户号码。
        /// Account number.
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// 账户哈希值（用于 API 请求）。
        /// Account hash value (used for API requests).
        /// </summary>
        public string HashValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// 嘉信理财账户详情。
    /// Charles Schwab account details.
    /// </summary>
    public class SchwabAccount
    {
        /// <summary>
        /// 账户号码。
        /// Account number.
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// 账户类型。
        /// Account type.
        /// </summary>
        public string AccountType { get; set; } = string.Empty;

        /// <summary>
        /// 账户余额。
        /// Account balance.
        /// </summary>
        public decimal CurrentBalance { get; set; }

        /// <summary>
        /// 可用现金。
        /// Available cash.
        /// </summary>
        public decimal AvailableCash { get; set; }

        /// <summary>
        /// 购买力。
        /// Buying power.
        /// </summary>
        public decimal BuyingPower { get; set; }

        /// <summary>
        /// 持仓列表。
        /// Positions list.
        /// </summary>
        public List<SchwabPosition> Positions { get; set; } = new();
    }

    /// <summary>
    /// 嘉信理财持仓。
    /// Charles Schwab position.
    /// </summary>
    public class SchwabPosition
    {
        /// <summary>
        /// 股票代码。
        /// Symbol.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 资产类型（EQUITY, OPTION, etc.）。
        /// Asset type (EQUITY, OPTION, etc.).
        /// </summary>
        public string AssetType { get; set; } = string.Empty;

        /// <summary>
        /// 持仓数量。
        /// Quantity.
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// 平均成本。
        /// Average cost.
        /// </summary>
        public decimal AverageCost { get; set; }

        /// <summary>
        /// 当前市场价格。
        /// Current market price.
        /// </summary>
        public decimal CurrentPrice { get; set; }

        /// <summary>
        /// 市场价值。
        /// Market value.
        /// </summary>
        public decimal MarketValue { get; set; }

        /// <summary>
        /// 未实现盈亏。
        /// Unrealized profit/loss.
        /// </summary>
        public decimal UnrealizedPnL { get; set; }

        /// <summary>
        /// 未实现盈亏百分比。
        /// Unrealized profit/loss percentage.
        /// </summary>
        public decimal UnrealizedPnLPercent { get; set; }
    }

    /// <summary>
    /// 嘉信理财订单。
    /// Charles Schwab order.
    /// </summary>
    public class SchwabOrder
    {
        /// <summary>
        /// 订单 ID。
        /// Order ID.
        /// </summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// 股票代码。
        /// Symbol.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 订单类型（MARKET, LIMIT, STOP, etc.）。
        /// Order type (MARKET, LIMIT, STOP, etc.).
        /// </summary>
        public string OrderType { get; set; } = string.Empty;

        /// <summary>
        /// 指令（BUY, SELL, etc.）。
        /// Instruction (BUY, SELL, etc.).
        /// </summary>
        public string Instruction { get; set; } = string.Empty;

        /// <summary>
        /// 数量。
        /// Quantity.
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// 价格（限价单）。
        /// Price (for limit orders).
        /// </summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// 订单状态。
        /// Order status.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 订单创建时间。
        /// Order creation time.
        /// </summary>
        public DateTimeOffset EnteredTime { get; set; }

        /// <summary>
        /// 订单关闭时间。
        /// Order close time.
        /// </summary>
        public DateTimeOffset? CloseTime { get; set; }

        /// <summary>
        /// 已成交数量。
        /// Filled quantity.
        /// </summary>
        public decimal FilledQuantity { get; set; }

        /// <summary>
        /// 剩余数量。
        /// Remaining quantity.
        /// </summary>
        public decimal RemainingQuantity { get; set; }
    }

    /// <summary>
    /// 嘉信理财市场数据报价。
    /// Charles Schwab market data quote.
    /// </summary>
    public class SchwabQuote
    {
        /// <summary>
        /// 股票代码。
        /// Symbol.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 资产类型。
        /// Asset type.
        /// </summary>
        public string AssetType { get; set; } = string.Empty;

        /// <summary>
        /// 最新价格。
        /// Last price.
        /// </summary>
        public decimal LastPrice { get; set; }

        /// <summary>
        /// 买价。
        /// Bid price.
        /// </summary>
        public decimal BidPrice { get; set; }

        /// <summary>
        /// 卖价。
        /// Ask price.
        /// </summary>
        public decimal AskPrice { get; set; }

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
        /// 成交量。
        /// Volume.
        /// </summary>
        public long Volume { get; set; }

        /// <summary>
        /// 开盘价。
        /// Open price.
        /// </summary>
        public decimal OpenPrice { get; set; }

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
        /// 收盘价。
        /// Close price.
        /// </summary>
        public decimal ClosePrice { get; set; }

        /// <summary>
        /// 涨跌额。
        /// Net change.
        /// </summary>
        public decimal NetChange { get; set; }

        /// <summary>
        /// 涨跌幅百分比。
        /// Net change percentage.
        /// </summary>
        public decimal NetChangePercent { get; set; }

        /// <summary>
        /// 报价时间。
        /// Quote time.
        /// </summary>
        public DateTimeOffset QuoteTime { get; set; }

        /// <summary>
        /// 交易时间。
        /// Trade time.
        /// </summary>
        public DateTimeOffset TradeTime { get; set; }
    }
}
