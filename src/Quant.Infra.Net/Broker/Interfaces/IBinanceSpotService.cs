using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Interfaces
{
    public interface IBinanceSpotService
    {
        public Task<IEnumerable<string>> GetSpotSymbolsAsync();
      
        public Task<Ohlcvs> GetOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, Shared.Model.ResolutionLevel resolutionLevel = Shared.Model.ResolutionLevel.Hourly);
    }
}
