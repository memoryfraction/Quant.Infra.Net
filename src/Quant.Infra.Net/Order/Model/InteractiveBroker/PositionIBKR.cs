namespace Quant.Infra.Net.Exchange.Model.InteractiveBroker
{
    public class PositionIBKR
    {
        public string Account { get; }
        public decimal Quantity { get; }
        public double AverageCost { get; }
    }
}