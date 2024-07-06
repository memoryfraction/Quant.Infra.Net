using System;

namespace Quant.Infra.Net
{
    public class OrderAbstract
    {
        public string OrderId { get; set; }
        public string Symbol { get; set; }
        public OrderType OrderType { get; set; }
        public OrderSide OrderSide { get; set; }
        public TimeInForce TimeInForce { get; set; } = TimeInForce.GoodTillCanceled;

        /// <summary>
        /// 基础资产的数量， e.g. 要买入或卖出100股某股票，设置Quantity为100。
        /// </summary>
        public decimal? Quantity { get; set; }

        public decimal? Price { get; set; }
    }



    public class OrderBinance: OrderAbstract
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
                      $"OrderType:{OrderType};" +
                      $"OrderSide:{OrderSide}; " +
                      $"TimeInForce:{TimeInForce}; " +
                      $"Quantity:{Quantity}; " +
                      $"QuoteOrderQty:{QuoteOrderQty}" +
                      $"Price:{Price}" +
                      $"OrderId:{OrderId}";
            return str;
        }
    }


    public class OrderIBKR: OrderAbstract
    {
        public override string ToString()
        {
            var str =
                      $"Symbol:{Symbol}; " +
                      $"OrderType:{OrderType};" +
                      $"OrderSide:{OrderSide}; " +
                      $"TimeInForce:{TimeInForce}; " +
                      $"Quantity:{Quantity}; " +
                      $"Price:{Price}" +
                      $"OrderId:{OrderId}";
            return str;
        }
    }


    public static class OrderFactory
    {
        public static OrderAbstract CreateOrder(string exchange)
        {
            switch (exchange.ToUpper())
            {
                case "BINANCE":
                    return new OrderBinance();
                case "INTERACTIVEBROKER":
                    return new OrderIBKR();
                default:
                    throw new ArgumentException("Unsupported exchange type.");
            }
        }
    }


}
