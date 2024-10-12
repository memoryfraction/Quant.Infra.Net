using Quant.Infra.Net.Portfolio.Models;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Threading.Tasks;


namespace Quant.Infra.Net.Account.Service
{
    public class InMemoryBinanceBrokerService: BrokerServiceBase
    {
        public new CryptoPortfolio Portfolio { get; set; } // 使用 new 隐藏基类属性


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

        public override void SetHoldings(Underlying underlying, decimal ratio)
        {
            // Todo: 查看当前持仓; 
            // todo: 计算目标持仓和当前持仓的差额
            // todo： 执行差额交易
            throw new NotImplementedException();
        }

        public override void Liquidate(Underlying underlying)
        {
            // 查看当前持仓; 
            var underlyingQuantity = Portfolio.GetUnderlyingQuantity(underlying);
            // Todo: 做反向交易;

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
