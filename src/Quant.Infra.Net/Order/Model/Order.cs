using Quant.Infra.Net.Shared.Model;
using System;

namespace Quant.Infra.Net
{
    /// <summary>
    /// 基础订单类，包含订单的基本属性和状态。
    /// </summary>
    public class OrderBase
    {
        public DateTime DateTimeUtc { get; set; }

        /// <summary>
        /// 订单的唯一标识符。
        /// </summary>
        public string OrderId { get; set; }

        /// <summary>
        /// 订单涉及的交易对或标的资产。
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// 订单执行类型，例如市场订单、限价订单等。
        /// </summary>
        public OrderExecutionType ExecutionType { get; set; }

        /// <summary>
        /// 订单的动作类型，例如买入或卖出。
        /// </summary>
        public OrderActionType ActionType { get; set; }

        /// <summary>
        /// 订单的有效时间类型，例如“GoodTillCanceled”（有效直至取消）。
        /// </summary>
        public TimeInForce TimeInForce { get; set; } = TimeInForce.GoodTillCanceled;

        /// <summary>
        /// 基础资产的数量，例如买入或卖出多少股票或比特币。
        /// <para>适用于所有订单类型，例如市场订单和限价订单。</para>
        /// </summary>
        public decimal? Quantity { get; set; }

        /// <summary>
        /// 订单的价格，例如每股股票的价格或每比特币的价格。
        /// </summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// 对于市场订单（MARKET orders），指定希望花费或收到的报价资产的金额。
        /// <para>当使用市场订单时，设置此属性来指定希望用多少报价资产购买或获得某个基础资产。</para>
        /// </summary>
        public decimal? QuoteOrderQty { get; set; }

        /// <summary>
        /// 订单状态，例如“New”（新订单）。
        /// </summary>
        public OrderStatus Status { get; set; } = OrderStatus.New;
    }

    public class OrderBinanceSpot : OrderBase
    {
        /*
            对于Binance Exchange;
            Market Order使用quantity指定用户想要以市场价格购买或出售的base asset的数量。
                例如，发送 BTCUSDT 的Market Order将指定用户购买或出售多少BTC。
            使用 quoteOrderQty 的 MARKET Order指定用户想要花费（购买时）或接收（出售时）报价资产的金额； 正确的数量将根据市场流动性和报价订单数量确定。
                以BTCUSDT为例：
                    在买入方面，订单将购买与 quoteOrderQty USDT 一样多的 BTC。
                    在卖出方面，订单将卖出接收 quoteOrderQty USDT 所需的 BTC。

           Mandantory field:
           symbol	STRING	YES
           side	ENUM	YES
           type	ENUM	YES

           Type 	              Additional mandatory parameters
           LIMIT	              timeInForce, quantity, price
           MARKET	              quantity or quoteOrderQty
           STOP_LOSS	          quantity, stopPrice or trailingDelta
           STOP_LOSS_LIMIT	      timeInForce, quantity, price, stopPrice or trailingDelta
           TAKE_PROFIT	          quantity, stopPrice or trailingDelta
           TAKE_PROFIT_LIMIT	  timeInForce, quantity, price, stopPrice or trailingDelta
           LIMIT_MAKER	quantity, price
         */

        /// <summary>
        /// QuoteOrderQty属性则用于市场订单（MARKET orders），表示您想要花费或收到的报价资产的具体金额。
        /// e.g. 当您创建市场订单时，如果您使用QuoteOrderQty，服务器会执行订单并尽可能多地成交，但最终的成交数量可能会与QuoteOrderQty略有不同1。
        /// </summary>
        public decimal? QuoteOrderQty { get; set; }

        public override string ToString()
        {
            var str =
                      $"Symbol:{Symbol}; " +
                      $"OrderActionType:{ActionType};" +
                      $"TimeInForce:{TimeInForce}; " +
                      $"Quantity:{Quantity}; " +
                      $"QuoteOrderQty:{QuoteOrderQty}" +
                      $"Price:{Price}" +
                      $"OrderId:{OrderId}";
            return str;
        }
    }

    public class OrderBinancePerpetualContract : OrderBase
    {
        /*
            对于Binance永续合约;
            Market Order使用quantity指定用户想要以市场价格购买或出售的base asset的数量。
                例如，发送 BTCUSDT 的Market Order将指定用户购买或出售多少BTC。
            使用 quoteOrderQty 的 MARKET Order指定用户想要花费（购买时）或接收（出售时）报价资产的金额； 正确的数量将根据市场流动性和报价订单数量确定。
                以BTCUSDT为例：
                    在买入方面，订单将购买与 quoteOrderQty USDT 一样多的 BTC。
                    在卖出方面，订单将卖出接收 quoteOrderQty USDT 所需的 BTC。

            Mandantory field:
            symbol	STRING	YES
            side	ENUM	YES
            type	ENUM	YES

            Type 	              Additional mandatory parameters
            LIMIT	              timeInForce, quantity, price
            MARKET	              quantity or quoteOrderQty
            STOP_LOSS	          quantity, stopPrice or trailingDelta
            STOP_LOSS_LIMIT	      timeInForce, quantity, price, stopPrice or trailingDelta
            TAKE_PROFIT	          quantity, stopPrice or trailingDelta
            TAKE_PROFIT_LIMIT	  timeInForce, quantity, price, stopPrice or trailingDelta
            LIMIT_MAKER	quantity, price
        */

        /// <summary>
        /// StopPrice属性用于止损订单（STOP_LOSS 和 STOP_LOSS_LIMIT）和止盈订单（TAKE_PROFIT 和 TAKE_PROFIT_LIMIT），
        /// 表示触发止损或止盈的价格。
        /// </summary>
        public decimal? StopPrice { get; set; }

        /// <summary>
        /// TrailingDelta属性用于止损（STOP_LOSS 和 STOP_LOSS_LIMIT）和止盈（TAKE_PROFIT 和 TAKE_PROFIT_LIMIT），
        /// 表示追踪止损的增量。
        /// </summary>
        public decimal? TrailingDelta { get; set; }

        /// <summary>
        /// Leveraged属性用于指定是否使用杠杆。
        /// </summary>
        public bool? Leveraged { get; set; }

        public override string ToString()
        {
            var str =
                      $"Symbol:{Symbol}; " +
                      $"OrderActionType:{ActionType}; " +
                      $"TimeInForce:{TimeInForce}; " +
                      $"Quantity:{Quantity}; " +
                      $"QuoteOrderQty:{QuoteOrderQty}; " +
                      $"Price:{Price}; " +
                      $"StopPrice:{StopPrice}; " +
                      $"TrailingDelta:{TrailingDelta}; " +
                      $"Leveraged:{Leveraged}; " +
                      $"OrderId:{OrderId}";
            return str;
        }
    }

    public class OrderIBKR : OrderBase
    {
        public override string ToString()
        {
            var str =
                      $"Symbol:{Symbol}; " +
                      $"OrderActionType:{ActionType};" +
                      $"TimeInForce:{TimeInForce}; " +
                      $"Quantity:{Quantity}; " +
                      $"Price:{Price}" +
                      $"OrderId:{OrderId}";
            return str;
        }
    }

    public static class OrderFactory
    {
        public static OrderBase CreateOrder(string exchange)
        {
            switch (exchange.ToUpper())
            {
                case "BINANCE":
                    return new OrderBinanceSpot();

                case "INTERACTIVEBROKER":
                    return new OrderIBKR();

                default:
                    throw new ArgumentException("Unsupported exchange type.");
            }
        }
    }
}