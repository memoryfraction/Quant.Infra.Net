using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;

namespace Quant.Infra.Net.SourceData.Model
{
    public class Ohlcvs
    {
        public string Symbol { get; set; }
        public ResolutionLevel Period { get; set; }
        public DateTime StartDateTimeUtc { get; set; }
        public DateTime EndDateTimeUtc { get; set; }

        public List<Ohlcv> OhlcvList { get; set; } = new List<Ohlcv>();
    }
}
