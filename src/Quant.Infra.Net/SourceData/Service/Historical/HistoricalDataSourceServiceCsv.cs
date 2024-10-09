using Microsoft.Data.Analysis;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Service.Historical
{
    public class HistoricalDataSourceServiceCsv : IHistoricalDataSourceService
    {
        public Currency BaseCurrency { get; set; }

        public Task<DataFrame> GetHistoricalDataFrameAsync(Underlying underlying, DateTime startDate, DateTime endDate, ResolutionLevel resolutionLevel)
        {
            throw new NotImplementedException();
        }

        public Task<List<Ohlcv>> GetOhlcvListAsync(Underlying underlying, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly, DateTime? startDt = null, DateTime? endDt = null, int limit = 1)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(Underlying underlying, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            throw new NotImplementedException();
        }
    }
}
