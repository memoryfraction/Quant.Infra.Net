using System;

namespace Quant.Infra.Net.SourceData.Model
{
    public class BasicOhlcv
    {
        public DateTime OpenDateTime { get; set; }
        public DateTime CloseDateTime { get; set; }

        public decimal Open { get; set; }

        public decimal High { get; set; }

        public decimal Low { get; set; }

        public decimal Close { get; set; }

        public decimal Volume { get; set; }

        public bool IsValid()
        {
            return OpenDateTime != default(DateTime) &&
             CloseDateTime != default(DateTime) &&
             Open != default(decimal) &&
             High != default(decimal) &&
             Low != default(decimal) &&
             Close != default(decimal) &&
             Volume != default(decimal);
        }
    }
    public class Ohlcv: BasicOhlcv
    {
        public decimal AdjustedClose { get; set; }
    }
}
