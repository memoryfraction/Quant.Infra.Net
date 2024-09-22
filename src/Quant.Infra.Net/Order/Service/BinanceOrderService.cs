using AutoMapper;
using Binance.Net.Enums;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Authentication;
using Polly;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quant.Infra.Net
{
    public class BinanceOrderService : IBinanceOrderService
    {
        private string _apiKey, _apiSecret;
        private readonly IMapper _mapper;

        public BinanceOrderService(IMapper mapper)
        {
            _mapper = mapper;
        }

        public async Task<BinanceOrderBase> CancelSpotOrderAsync(string symbol, long orderId, int retryAttempts = 3)
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.CancelOrderAsync(symbol, orderId), retryAttempts);
                return result.Data;
            }
        }

        ///
        public async Task<BinancePlacedOrder> PlaceSpotOrderAsync(string symbol, OrderSide orderSide, OrderActionType spotOrderType, decimal? quantity, decimal? quoteQuantity, decimal? price = null, int retryCount = 3)
        {
            var binanceOrderSide = _mapper.Map<Binance.Net.Enums.OrderSide>(orderSide);
            var binanceOrderType = _mapper.Map<Binance.Net.Enums.SpotOrderType>(spotOrderType);
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.PlaceOrderAsync(symbol, binanceOrderSide, binanceOrderType, quantity: quantity, price: price, quoteQuantity: quoteQuantity), retryCount);
                return result.Data;
            }
        }

        public async Task<BinancePlacedOrder> PlaceMarginOrderAsync(string symbol, OrderSide orderSide, OrderActionType spotOrderType, decimal? quantity, decimal? quoteQuantity, decimal? price = null, int retryCount = 3)
        {
            var binanceOrderSide = _mapper.Map<Binance.Net.Enums.OrderSide>(orderSide);
            var binanceOrderType = _mapper.Map<Binance.Net.Enums.SpotOrderType>(spotOrderType);
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.PlaceMarginOrderAsync(symbol, binanceOrderSide, binanceOrderType, quantity: quantity, price: price, quoteQuantity: quoteQuantity), retryCount);
                return result.Data;
            }
        }

        public async Task<IEnumerable<BinanceOrder>> GetAllSpotOpenOrdersAsync(string symbol = null, int retryAttempts = 3)
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.GetOpenOrdersAsync(symbol), retryAttempts);
                return result.Data;
            }
        }

        public async Task<BinanceOrder> GetSpotOrderAsync(string symbol, long orderId, int retryAttempts = 3)
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.GetOrderAsync(symbol, orderId), retryAttempts);
                return result.Data;
            }
        }

        public async Task<BinanceReplaceOrderResult> ReplaceSpotOrderAsync(string symbol, OrderSide orderSide, OrderActionType orderType, CancelReplaceMode cancelReplaceMode, long? cancelOrderId = null, string? cancelClientOrderId = null, string? newCancelClientOrderId = null, string? newClientOrderId = null, decimal? quantity = null, decimal? quoteQuantity = null, decimal? price = null, TimeInForce? timeInForce = null, decimal? stopPrice = null, decimal? icebergQty = null, OrderResponseType? orderResponseType = null, int? trailingDelta = null, int? strategyId = null, int? strategyType = null, CancelRestriction? cancelRestriction = null, int? receiveWindow = null, CancellationToken ct = default(CancellationToken), int retryAttempts = 3)
        {
            var binanceOrderSide = _mapper.Map<Binance.Net.Enums.OrderSide>(orderSide);
            var binanceOrderType = _mapper.Map<Binance.Net.Enums.SpotOrderType>(orderType);
            var binanceCancelReplaceMode = _mapper.Map<Binance.Net.Enums.CancelReplaceMode>(cancelReplaceMode);
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.ReplaceOrderAsync(symbol: symbol, side: binanceOrderSide, type: binanceOrderType, cancelOrderId: cancelOrderId, cancelReplaceMode: binanceCancelReplaceMode), retryAttempts);
                return result.Data;
            }
        }

        public void SetBinanceCredential(string apiKey, string apiSecret)
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;

            Binance.Net.Clients.BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
            });
        }

        private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> operation, int retryCount)
        {
            var policy = Policy.Handle<Exception>().RetryAsync(retryCount);
            return await policy.ExecuteAsync(operation);
        }

        public async Task<IEnumerable<BinanceOrderBase>> CancelAllOrdersAsync(string symbol, int retryAttempts = 3)
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var ordersResponse = await client.SpotApi.Trading.CancelAllOrdersAsync(symbol);
                return ordersResponse.Data;
            }
        }

        public async Task<IEnumerable<string>> GetAllSymbolsAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var exchangeInfo = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();
                var symbols = exchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
                return symbols;
            }
        }

        public async Task<decimal> GetSubAccountTotalAssetOfBtcAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var marginAccount = await client.SpotApi.Account.GetMarginAccountInfoAsync();
                var totalAssetOfBtc = marginAccount.Data.TotalAssetOfBtc; // exchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
                return totalAssetOfBtc;
            }
        }

        public Task LiquidateAsync(string symbol)
        {
            throw new NotImplementedException();
        }

        public Task LiquidateAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<BinancePositionDetailsUsdt>> GetHoldingPositionAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var account = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
                var position = await client.UsdFuturesApi.Account.GetPositionInformationAsync();
                var holdingPositions = position.Data.Where(x => x.Quantity != 0).Select(x => x);
                return holdingPositions;
            }
        }

        public async Task<BinanceUsdFuturesOrder> PlaceUsdFutureOrderAsync(string symbol, OrderSide orderSide, decimal quantity, PositionSide positionSide, FuturesOrderType orderType = FuturesOrderType.Market)
        {
            var binanceOrderSide = _mapper.Map<Binance.Net.Enums.OrderSide>(orderSide);
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var response = await client.UsdFuturesApi.Trading.PlaceOrderAsync(
                    symbol: symbol,
                    side: binanceOrderSide, // 开关仓此信号需要相反
                    type: orderType,
                    quantity: Math.Abs(quantity), // 关仓数量需要与开仓数量一致， 总是正数
                    positionSide: positionSide    // LONG/SHORT是对冲模式， 多头开关都用LONG, 空头开关都用SHORT
                    );
                if (response.Success)
                    return response.Data;
                else
                    return null;
            }
        }

        public async Task<IEnumerable<BinancePositionDetailsUsdt>> GetHoldingPositionAsync(string symbol)
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var account = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
                var position = await client.UsdFuturesApi.Account.GetPositionInformationAsync();
                var holdingPositions = position.Data.Where(x => x.Quantity != 0).Select(x => x);
                var holdingSymbolPosition = holdingPositions.Where(x => x.Symbol.ToLower() == symbol.ToLower());
                return holdingSymbolPosition;
            }
        }

        public async Task<BinanceFuturesAccountInfoV3> GetBinanceFuturesAccountInfoAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                // Margin Account Balance
                var accountInfo = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
                if (accountInfo.Success)
                    return accountInfo.Data;
                else
                    return null;
            }
        }
    }
}