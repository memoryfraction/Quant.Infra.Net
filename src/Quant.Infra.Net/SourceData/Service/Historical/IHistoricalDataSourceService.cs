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

        Task<DataFrame> GetHistoricalDataFrameAsync(
            Underlying underlying, 
            DateTime startDate, 
            DateTime endDate, 
            ResolutionLevel resolutionLevel);


        Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            DateTime startDt,
            DateTime endDt,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly
        );
    }

    public interface IHistoricalDataSourceServiceCryptoBinance : IHistoricalDataSourceService
    {
        /// <summary>
        /// 从Binance取数据, 定义endDt和limit(数量)和ResolutionLevel
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="endDt"></param>
        /// <param name="limit">无上限，可以循环操作</param>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            DateTime endDt,
            int limit,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly
            );
    }


    public interface IHistoricalDataSourceServiceCryptoMySql : IHistoricalDataSourceService
    {
        
        // 默认方法表示现货Spot， 如果需要其他的数据，可以此处添加;
    }


    public interface IHistoricalDataSourceServiceTraditionalFinance : IHistoricalDataSourceService
    {
        /// <summary>
        /// 从Binance取数据, 定义endDt和limit(数量)和ResolutionLevel
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="endDt"></param>
        /// <param name="limit">无上限，可以循环操作</param>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            DateTime endDt,
            int limit,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly
            );
    }
}
