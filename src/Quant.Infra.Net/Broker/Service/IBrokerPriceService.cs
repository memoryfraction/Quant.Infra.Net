using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Service
{
    public interface IBrokerService
    {
        /// <summary>
        /// 获取指定标的资产的最新价格。
        /// </summary>
        /// <param name="underlying">标的资产，例如股票、期货、加密货币等。</param>
        /// <returns>返回该标的资产的最新价格。</returns>
        Task<decimal> GetLatestPriceAsync(Underlying underlying);


        /// <summary>
        /// 基础货币，用于定价或汇率计算。
        /// </summary>
        Currency BaseCurrency { get; set; }

        Task<Ohlcv> GetOhlcvAsync(string symbol, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly,
            AssetType assetType = AssetType.CryptoSpot,
            DateTime? startDt = null,
            DateTime? endDt = null,
            int limit = 1);
    }
}
