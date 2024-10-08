﻿using Quant.Infra.Net.Portfolio.Models;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Threading.Tasks;


namespace Quant.Infra.Net.Account.Service
{
    public class InMemoryBinanceBrokerService: BrokerServiceBase
    {
        public CryptoPortfolio Portfolio { get; set; }

        /// <summary>
        /// 构造函数初始化 CryptoPerpetualContractPortfolio
        /// </summary>
        /// <param name="portfolioBase"></param>
        public InMemoryBinanceBrokerService(PortfolioBase portfolioBase)
        {
            Portfolio = portfolioBase as CryptoPortfolio;
            // Portfolio.InitCapital = initCapital;
        }

        public override Task<decimal> GetLatestPriceAsync(Underlying underlying)
        {
            throw new NotImplementedException();
        }

        public override Task SetHoldingsAsync(Underlying underlying, decimal ratio)
        {
            throw new NotImplementedException();
        }

        public override Task<decimal> GetHoldingAsync(Underlying underlying)
        {
            throw new NotImplementedException();
        }

        public override Task<decimal> GetMarketValueAsync(Underlying underlying)
        {
            throw new NotImplementedException();
        }

        public override Task<decimal> GetTotalMarketValueAsync()
        {
            throw new NotImplementedException();
        }
    }
}
