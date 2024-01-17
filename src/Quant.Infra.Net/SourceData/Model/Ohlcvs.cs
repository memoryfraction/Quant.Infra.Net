using System;
using System.Collections.Generic;
using System.Text;
using Quant.Infra.Net.Shared.Model;

namespace Quant.Infra.Net.SourceData.Model
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
