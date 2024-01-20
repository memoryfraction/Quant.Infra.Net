namespace Quant.Infra.Net
{
    public class Order
    {
        public string Symbol { get; set; }
        public string OrderId { get; set; }
        public SpotOrderType OrderType { get; set; }
        public OrderSide OrderSide { get; set; }
        public TimeInForce TimeInForce { get; set; }
    }
}
