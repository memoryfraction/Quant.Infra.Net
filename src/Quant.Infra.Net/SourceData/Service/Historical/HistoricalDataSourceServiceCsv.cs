using Microsoft.Data.Analysis;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Service.Historical
{
    /// <summary>
    /// CSV 历史数据源服务实现。
    /// Historical data source service backed by local CSV files.
    /// </summary>
    public class HistoricalDataSourceServiceCsv : IHistoricalDataSourceService
    {
        /// <summary>
        /// 基础货币，默认为 USD / Base currency, default is USD.
        /// </summary>
        public Currency BaseCurrency { get; set; }

        /// <summary>
        /// 从 CSV 文件获取历史 DataFrame。此方法尚未实现。
        /// Retrieve historical data as a DataFrame from CSV files. Not yet implemented.
        /// </summary>
        /// <param name="underlying">标的资产 / The underlying asset.</param>
        /// <param name="startDate">起始日期 / Start date of the range.</param>
        /// <param name="endDate">截止日期 / End date of the range.</param>
        /// <param name="resolutionLevel">时间粒度 / Resolution level.</param>
        public Task<DataFrame> GetHistoricalDataFrameAsync(Underlying underlying, DateTime startDate, DateTime endDate, ResolutionLevel resolutionLevel)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取 OHLCV K 线列表（分页模式）。此方法尚未实现。
        /// Fetch OHLCV list in paginated mode. Not yet implemented.
        /// </summary>
        /// <param name="underlying">标的资产 / The underlying asset.</param>
        /// <param name="resolutionLevel">时间粒度 / Resolution level (default: Hourly).</param>
        /// <param name="startDt">起始日期（可选）/ Optional start date.</param>
        /// <param name="endDt">截止日期（可选）/ Optional end date.</param>
        /// <param name="limit">返回数量限制 / Number of records to return.</param>
        public Task<List<Ohlcv>> GetOhlcvListAsync(Underlying underlying, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly, DateTime? startDt = null, DateTime? endDt = null, int limit = 1)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取 OHLCV K 线列表（日期范围模式）。此方法尚未实现。
        /// Fetch OHLCV list for a date range. Not yet implemented.
        /// </summary>
        /// <param name="underlying">标的资产 / The underlying asset.</param>
        /// <param name="startDt">起始日期 / Start date.</param>
        /// <param name="endDt">截止日期 / End date.</param>
        /// <param name="resolutionLevel">时间粒度（默认 Hourly）/ Resolution level (default: Hourly).</param>
        public Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(Underlying underlying, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            throw new NotImplementedException();
        }
    }
}
