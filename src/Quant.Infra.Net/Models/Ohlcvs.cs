using System;
using System.Collections.Generic;
using System.Text;

namespace Quant.Infra.Net.Models
{
    public class Ohlcvs
    {
        public string Symbol { get; set; }
        public Period Period { get; set; }
        public DateTime StartDateTimeUtc { get; set; }
        public DateTime EndDateTimeUtc { get; set; }

        public List<Ohlcv> OhlcvList { get; set; } = new List<Ohlcv>();
    }
}
