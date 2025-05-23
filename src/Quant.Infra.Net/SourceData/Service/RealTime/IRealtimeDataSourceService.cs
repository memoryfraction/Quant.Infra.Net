using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Service.RealTime
{
    public interface IRealtimeDataSourceService
    {
        /// <summary>
        /// 获取指定标的资产的最新价格。
        /// </summary>
        /// <param name="underlying">标的资产，例如股票、期货、加密货币等。</param>
        /// <returns>返回该标的资产的最新价格。</returns>
        Task<decimal> GetLatestPriceAsync(Underlying underlying);


        /// <summary>
        /// 从Binance取数据, 定义endDt和limit(数量)和ResolutionLevel
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="endDt"></param>
        /// <param name="limit"></param>
        /// <param name="resolutionLevel">无上限，可以循环操作</param>
        /// <returns></returns>
        Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            DateTime endDt,
            int limit,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly);

        /// <summary>
        /// 基础货币，用于定价或汇率计算。
        /// </summary>
        Currency BaseCurrency { get; set; }
    }

    public interface IRealtimeDataSourceServiceCrypto : IRealtimeDataSourceService
    {

    }

    public interface IRealtimeDataSourceServiceTraditionalFinance : IRealtimeDataSourceService
    {

    }
}
