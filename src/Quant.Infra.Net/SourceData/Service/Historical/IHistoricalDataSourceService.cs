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

        Task<DataFrame> GetHistoricalDataFrameAsync(Underlying underlying, DateTime startDate, DateTime endDate, ResolutionLevel resolutionLevel);
    }

    public interface IHistoricalDataSourceServiceCryptoBinance : IHistoricalDataSourceService
    {
        // 默认方法表示现货Spot， 如果需要其他的数据，可以此处添加;
        /// <summary>
        /// 调用Binance API， 获取历史数据
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="resolutionLevel"></param>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public Task<List<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly,
            DateTime? startDt = null,
            DateTime? endDt = null,
            int limit = 1);
    }

    public interface IHistoricalDataSourceServiceCryptoMySql : IHistoricalDataSourceService
    {
        /// <summary>
        /// 读取MySql，获取历史数据
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        Task<List<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            DateTime startDt,
            DateTime endDt,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly);
        
        // 默认方法表示现货Spot， 如果需要其他的数据，可以此处添加;
    }

    public interface IHistoricalDataSourceServiceTraditionalFinance : IHistoricalDataSourceService
    {

    }
}
