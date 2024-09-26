using Microsoft.Data.Analysis;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Service.Historical
{
    public interface IHistoricalDataSourceService
    {
        /// <summary>
        /// 基础货币，用于定价或汇率计算。
        /// </summary>
        Currency BaseCurrency { get; set; }


        Task<List<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly,
            DateTime? startDt = null,
            DateTime? endDt = null,
            int limit = 1);

        Task<DataFrame> GetHistoricalDataFrameAsync(Underlying underlying, DateTime startDate, DateTime endDate, ResolutionLevel resolutionLevel);
    }
}
