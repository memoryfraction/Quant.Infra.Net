using System;

namespace Quant.Infra.Net.Broker.Model
{
    public class OpenOrder
    {
        public string Symbol { get; set; }
        public decimal? Quantity { get; set; }
        public decimal FilledQuantity { get; set; }
        public Guid OrderId { get; set; }
        public string Status { get; set; } // New, PartiallyFilled, Filled, Cancelled, etc.
        public DateTime? CreatedAtUtc { get; set; }
    }
}
