using InterReact;
using Quant.Infra.Net.Exchange.Model.InteractiveBroker;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace Quant.Infra.Net.Exchange.Service
{
    /// <summary>
    /// Interactive Brokers 服务实现，通过 InterReact 库连接 TWS/Gateway。
    /// Interactive Brokers service implementation connecting to TWS/Gateway via the InterReact library.
    /// </summary>
    public class IBKRService : IIBKRService
    {
        private string _apiKey, _apiSecret;
        private IInterReactClient? _client;

        /// <summary>
        /// 网络请求重试策略，应对暂时性连接失败。
        /// Network request retry policy to handle transient connection failures.
        /// </summary>
        private readonly AsyncRetryPolicy _retryPolicy;

        public IBKRService()
        {
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retries => System.TimeSpan.FromSeconds(System.Math.Pow(2, retries)),
                    onRetry: (response, span, retry, context) =>
                    {
                        Quant.Infra.Net.Shared.Service.UtilityService.LogAndWriteLine(
                            $"[IBKR Retry] Attempt {{retry}}/3 after {{span.TotalSeconds}}s - Error: {{response.Exception?.Message}}");
                    }
                );

            if (_client == null)
                _client = InterReactClient.ConnectAsync().Result;
        }

        /// <summary>
        /// 获取 Interactive Brokers 账户摘要（保证金、资金、持仓概览）。
        /// Retrieve the IBKR account summary (margin, funds, position overview).
        /// </summary>
        /// <returns>账户摘要数据 / Account summary data.</returns>
        public Task<AccountSummaryIBKR> GetAccountSummaryAsync()
        {
            // Todo GetAccountSummaryAsync
            throw new NotImplementedException();
        }

        /// <summary>
        /// 获取 Interactive Brokers 当前持仓信息。
        /// Retrieve the current IBKR position information.
        /// </summary>
        /// <returns>持仓数据 / Position data.</returns>
        public Task<PositionIBKR> GetPositionAsync()
        {
            // Todo GetPositionAsync
            throw new NotImplementedException();
        }

        /// <summary>
        /// 通过 Interactive Brokers 下单（限价单或市价单）。
        /// Place an order through Interactive Brokers (limit or market order).
        /// </summary>
        /// <param name="order">订单信息 / Order information.</param>
        /// <param name="exchange">交易所 / Exchange (default SMART).</param>
        /// <param name="securityType">证券类型 / Security type.</param>
        /// <param name="currency">货币 / Currency.</param>
        /// <returns>订单ID / Order ID.</returns>
        /// <exception cref="ArgumentNullException">当 order 为 null 时抛出 / Thrown when order is null.</exception>
        /// <exception cref="ArgumentException">当订单参数无效时抛出 / Thrown when order parameters are invalid.</exception>
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

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                // Ensure mapper and client remain available
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
            });
        }
    }
}
