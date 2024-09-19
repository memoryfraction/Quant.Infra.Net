using Quant.Infra.Net.Shared.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Account.Service
{
    public abstract class AbstractBrokerService
    {
        public virtual Currency BaseCurrency { get; set; } = Currency.USD;

        public abstract Task<List<string>> GetSymbolListAsync();

        /// <summary>
        /// 异步设置持仓比例
        /// Asynchronously set holdings ratio for a specific symbol
        /// </summary>
        /// <param name="symbol">股票或资产的代码</param>
        /// <param name="ratio">持仓比例</param>
        public abstract Task SetHoldingsAsync(string symbol, AssetType asssetType, decimal ratio);


        /// <summary>
        /// 异步获取持仓比例
        /// Asynchronously get holdings shares for a specific symbol
        /// </summary>
        /// <param name="symbol">股票或资产的代码</param>
        /// <returns>返回持有该股票或资产的份额</returns>
        public abstract Task<decimal> GetHoldingAsync(string symbol, AssetType asssetType);


        /// <summary>
        /// 异步获取指定股票或资产的市场价值
        /// Asynchronously get the market value for a specific symbol
        /// </summary>
        /// <param name="symbol">股票或资产的代码</param>
        /// <returns>返回该股票或资产的市场价值</returns>
        public abstract Task<decimal> GetMarketValueAsync(string symbol, AssetType asssetType);


        /// <summary>
        /// 异步获取总的市场价值
        /// Asynchronously get the total market value of all holdings
        /// </summary>
        /// <returns>返回市场的总价值</returns>
        public abstract Task<decimal> GetTotalMarketValueAsync();

    }
}
