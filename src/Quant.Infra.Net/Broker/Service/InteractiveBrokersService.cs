using Binance.Net.Clients;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Account.Service
{
    public class InteractiveBrokersService : BrokerServiceBase
    {
        private readonly BinanceRestClient _binanceRestClient;

        public override Currency BaseCurrency { get; set; } = Currency.USD;

        /// <summary>
        /// 异步获取所有交易对的列表
        /// Asynchronously get the list of all trading pairs
        /// </summary>
        /// <returns>返回交易对的列表</returns>
        public override async Task<IEnumerable<string>> GetSymbolListAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<decimal> GetLatestPriceAsync(Underlying underlying)
        {
            throw new NotImplementedException();
        }

        public override void SetHoldings(Underlying underlying, decimal ratio)
        {
            throw new NotImplementedException();
        }

        public override void Liquidate(Underlying underlying)
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// 异步获取指定资产的持仓份额
        /// Asynchronously get holdings shares for a specific asset
        /// </summary>
        /// <param name="symbol">资产的代码</param>
        /// <param name="assetType">资产类型</param>
        /// <returns>返回持有该资产的份额</returns>
        public override async Task<decimal> GetHoldingAsync(Underlying underlying)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 异步获取指定资产的市场价值
        /// Asynchronously get the market value for a specific asset
        /// </summary>
        /// <param name="symbol">资产的代码</param>
        /// <param name="assetType">资产类型</param>
        /// <returns>返回该资产的市场价值</returns>
        public override async Task<decimal> GetMarketValueAsync(Underlying underlying)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 异步获取所有持仓的总市场价值
        /// Asynchronously get the total market value of all holdings
        /// </summary>
        /// <returns>返回市场的总价值</returns>
        public override async Task<decimal> GetTotalMarketValueAsync()
        {
            throw new NotImplementedException();
        }
    }
}