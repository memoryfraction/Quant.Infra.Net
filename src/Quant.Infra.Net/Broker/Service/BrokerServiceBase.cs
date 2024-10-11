using Quant.Infra.Net.Portfolio.Models;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Account.Service
{
    public abstract class BrokerServiceBase
    {
        public virtual Currency BaseCurrency { get; set; } = Currency.USD;
        public virtual PortfolioBase Portfolio { get; set; } // 定义一个通用的 Portfolio 属性

        public virtual Task<IEnumerable<string>> GetSymbolListAsync()
        {
            throw new NotImplementedException();
        }

        public virtual Task<IEnumerable<string>> GetSpotSymbolListAsync()
        {
            throw new NotImplementedException();
        }

        public virtual Task<IEnumerable<string>> GetUsdFuturesSymbolListAsync()
        {
            throw new NotImplementedException();
        }

        public virtual Task<IEnumerable<string>> GetCoinFuturesSymbolListAsync()
        {
            throw new NotImplementedException();
        }

        public abstract Task<decimal> GetLatestPriceAsync(Underlying underlying);

        /// <summary>
        /// 异步设置持仓比例
        /// Asynchronously set holdings ratio for a specific symbol
        /// </summary>
        /// <param name="symbol">股票或资产的代码</param>
        /// <param name="ratio">持仓比例</param>
        public abstract void SetHoldings(Underlying underlying, decimal ratio);

        public abstract void Liquidate(Underlying underlying);

        /// <summary>
        /// 异步获取持仓比例
        /// Asynchronously get holdings shares for a specific symbol
        /// </summary>
        /// <param name="symbol">股票或资产的代码</param>
        /// <returns>返回持有该股票或资产的份额</returns>
        public abstract Task<decimal> GetHoldingAsync(Underlying underlying);

        /// <summary>
        /// 异步获取指定股票或资产的市场价值
        /// Asynchronously get the market value for a specific symbol
        /// </summary>
        /// <param name="symbol">股票或资产的代码</param>
        /// <returns>返回该股票或资产的市场价值</returns>
        public abstract Task<decimal> GetMarketValueAsync(Underlying underlying);

        /// <summary>
        /// 异步获取总的市场价值
        /// Asynchronously get the total market value of all holdings
        /// </summary>
        /// <returns>返回市场的总价值</returns>
        public abstract Task<decimal> GetTotalMarketValueAsync();
    }
}