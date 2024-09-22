using Quant.Infra.Net.Portfolio.Models;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Threading.Tasks;


namespace Quant.Infra.Net.Account.Service
{
    public class InMemoryBrokerService: BrokerServiceBase
    {
        public PortfolioBase Portfolio { get; set; }

        // 构造函数初始化 CryptoPerpetualContractPortfolio
        public InMemoryBrokerService(decimal initCapital)
        {
            Portfolio = new CryptoPerpetualContractPortfolio();
            Portfolio.InitCapital = initCapital;
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
