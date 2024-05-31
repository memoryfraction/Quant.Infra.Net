using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Exchange.Service
{
    public class IBKRService : IIBKRService
    {
        public Task PlaceMarketOrderAsync(Order order, string exchange = "SMART", ContractSecurityType securityType = ContractSecurityType.Stock, Currency currency = Currency.USD)
        {
            throw new NotImplementedException();
        }
    }
}
