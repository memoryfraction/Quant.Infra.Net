using System;
using System.Collections.Generic;
using System.Text;

namespace Quant.Infra.Net.Models
{
    public class Ohlcv
    {
        public DateTime DateTime { get; set; }

        public decimal Open { get; set; }

        public decimal High { get; set; }

        public decimal Low { get; set; }

        public decimal Close { get; set; }

        public long Volume { get; set; }

        public decimal AdjustedClose { get; set; }
    }
}
