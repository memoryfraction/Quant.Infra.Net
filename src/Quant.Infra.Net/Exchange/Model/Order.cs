namespace Quant.Infra.Net
{
    public class Order
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
        public string Symbol { get; set; }
        public SpotOrderType OrderType { get; set; }
        public OrderSide OrderSide { get; set; }
        public TimeInForce TimeInForce { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? QuoteOrderQty { get; set; }
        public decimal? Price { get; set; }
        public string OrderId { get; set; }

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
}
