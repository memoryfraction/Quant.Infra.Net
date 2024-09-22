using AutoMapper;
using InterReact;
using Quant.Infra.Net.Exchange.Model.InteractiveBroker;
using Quant.Infra.Net.Shared.Model;
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
            if (_client == null)
                _client = InterReactClient.ConnectAsync().Result;
        }

        public Task<AccountSummaryIBKR> GetAccountSummaryAsync()
        {
            // Todo GetAccountSummaryAsync
            throw new NotImplementedException();
        }

        public Task<PositionIBKR> GetPositionAsync()
        {
            // Todo GetPositionAsync
            throw new NotImplementedException();
        }

        public async Task<int> PlaceOrderAsync(
            OrderBase order,
            string exchange = "SMART",
            Quant.Infra.Net.Shared.Model.ContractSecurityType securityType = Quant.Infra.Net.Shared.Model.ContractSecurityType.Stock,
            Quant.Infra.Net.Shared.Model.Currency currency = Quant.Infra.Net.Shared.Model.Currency.USD
            )
        {
            // https://github.com/dshe/InterReact/blob/master/InterReact.Tests/SystemTests/Orders/PlaceOrderTests.cs

            _client = await InterReactClient.ConnectAsync();
            InterReact.Contract interReactContract = new()
            {
                SecurityType = InterReact.ContractSecurityType.Stock,
                Symbol = order.Symbol,
                Currency = currency.ToString(),
                Exchange = exchange
            };

            int orderId = _client.Request.GetNextId();
            InterReact.Order interReactOrder = new InterReact.Order();
            if (order.ExecutionType == OrderExecutionType.Limit)
            {
                interReactOrder = new()
                {
                    Action = order.ActionType.ToString(),
                    TotalQuantity = order.Quantity == null ? 0 : order.Quantity.Value,
                    OrderType = OrderTypes.Limit,
                    LimitPrice = order.Price == null ? 0.0 : (double)order.Price.Value,
                    OutsideRegularTradingHours = true // 允许盘前盘后成交
                };
            }
            else if (order.ExecutionType == OrderExecutionType.Market)
            {
                interReactOrder = new InterReact.Order()
                {
                    Action = order.ActionType.ToString(),
                    TotalQuantity = order.Quantity == null ? 0.0m : order.Quantity.Value, // 需要测试结果;
                    OrderType = OrderTypes.Market,
                    OutsideRegularTradingHours = true // 允许盘前盘后成交
                };
            }
            else
            {
                throw new ArgumentException("Invalid order type");
            }
            _client.Request.PlaceOrder(orderId, interReactOrder, interReactContract);
            await _client.DisposeAsync();
            return orderId;
        }
    }
}