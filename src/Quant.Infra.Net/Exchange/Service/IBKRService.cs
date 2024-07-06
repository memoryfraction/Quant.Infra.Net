using AutoMapper;
using InterReact;
using System;
using System.Threading.Tasks;


namespace Quant.Infra.Net.Exchange.Service
{
    public class IBKRService : IIBKRService
    {
        private string _apiKey, _apiSecret;
        private IMapper _mapper;
        private IInterReactClient? _client;

        public IBKRService(IMapper mapper)
        {
            _mapper = mapper;
            if(_client == null)
                _client = InterReactClient.ConnectAsync().Result;
        }

        public Task PlaceMarketOrderAsync(
            Order order, string exchange = "SMART", 
            Quant.Infra.Net.Shared.Model.ContractSecurityType securityType = Quant.Infra.Net.Shared.Model.ContractSecurityType.Stock, 
            Quant.Infra.Net.Shared.Model.Currency currency = Quant.Infra.Net.Shared.Model.Currency.USD
            )
        {
            // https://github.com/dshe/InterReact/blob/master/InterReact.Tests/SystemTests/Orders/PlaceOrderTests.cs

            return Task.Run(() =>
            {
                // Your existing code...

                InterReact.Contract interReactContract = new()
                {
                    SecurityType = InterReact.ContractSecurityType.Stock,
                    Symbol = "AMZN",
                    Currency = "USD",
                    Exchange = "SMART"
                };

                int orderId = _client.Request.GetNextId();

                InterReact.Order interReactOrder = new()
                {
                    //todo need change
                    Action = OrderAction.Buy,
                    TotalQuantity = 100,
                    OrderType = OrderTypes.Market
                };

                _client.Request.PlaceOrder(orderId, interReactOrder, interReactContract);
            });

 
        }

        

    }
}
