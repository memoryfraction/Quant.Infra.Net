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
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
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

            if (order == null) throw new ArgumentNullException(nameof(order));
            if (string.IsNullOrWhiteSpace(order.Symbol)) throw new ArgumentException("order.Symbol must not be null or empty.", nameof(order));

            // Ensure mapper and client remain available
            if (_mapper == null) throw new InvalidOperationException("IMapper is not initialized.");

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
                if (order.Quantity == null || order.Quantity <= 0) throw new ArgumentException("order.Quantity must be positive for limit orders.", nameof(order));
                if (order.Price == null || order.Price <= 0) throw new ArgumentException("order.Price must be positive for limit orders.", nameof(order));

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
                if (order.Quantity == null || order.Quantity == 0) throw new ArgumentException("order.Quantity must not be zero for market orders.", nameof(order));

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