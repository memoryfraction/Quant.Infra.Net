using System;

namespace Quant.Infra.Net.SourceData.Model
{
    public class BasicOhlcv
    {
        public DateTime DateTime { get; set; }

        public decimal Open { get; set; }

        public decimal High { get; set; }

        public decimal Low { get; set; }

        public decimal Close { get; set; }

        public decimal Volume { get; set; }
    }
    public class Ohlcv: BasicOhlcv
    {
        public decimal AdjustedClose { get; set; }
    }
}
