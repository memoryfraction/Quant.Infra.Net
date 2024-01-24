using AutoMapper;
using Binance.Net.Enums;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Authentication;
using Polly;
using System;
using System.Collections.Generic;
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
            //_apiKey    = configuration["Exchange:apiKey"]; 
            //_apiSecret = configuration["Exchange:apiSecret"];
            _mapper = mapper;

            //Binance.Net.Clients.BinanceRestClient.SetDefaultOptions(options =>
            //{
            //    options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
            //});
        }

      
        public async Task<BinanceOrderBase> CancelSpotOrderAsync(string symbol, long orderId, int retryAttempts = 3)
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.CancelOrderAsync(symbol, orderId), retryAttempts);
                return result.Data;
            }
        }


        public async Task<BinancePlacedOrder> CreateSpotOrderAsync(string symbol, OrderSide orderSide, SpotOrderType spotOrderType, decimal? quantity, decimal? quoteQuantity, decimal? price = null, int retryCount = 3)
        {
            var binanceOrderSide = _mapper.Map<Binance.Net.Enums.OrderSide>(orderSide);
            var binanceOrderType = _mapper.Map<Binance.Net.Enums.SpotOrderType>(spotOrderType);
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.PlaceOrderAsync(symbol, binanceOrderSide, binanceOrderType, quantity: quantity, price: price, quoteQuantity:quoteQuantity), retryCount);
                return result.Data;
            }
        }

        public async Task<IEnumerable<BinanceOrder>> GetAllSpotOpenOrdersAsync(string symbol=null, int retryAttempts = 3)
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.GetOpenOrdersAsync(symbol),retryAttempts);
                return result.Data;
            }
        }

        public async Task<BinanceOrder> GetSpotOrderAsync(string symbol, long orderId, int retryAttempts = 3)
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.GetOrderAsync(symbol,orderId), retryAttempts);
                return result.Data;
            }
        }

        public async Task<BinanceReplaceOrderResult> ReplaceSpotOrderAsync(string symbol, OrderSide orderSide, SpotOrderType orderType, CancelReplaceMode cancelReplaceMode, long? cancelOrderId = null, string? cancelClientOrderId = null, string? newCancelClientOrderId = null, string? newClientOrderId = null, decimal? quantity = null, decimal? quoteQuantity = null, decimal? price = null, TimeInForce? timeInForce = null, decimal? stopPrice = null, decimal? icebergQty = null, OrderResponseType? orderResponseType = null, int? trailingDelta = null, int? strategyId = null, int? strategyType = null, CancelRestriction? cancelRestriction = null, int? receiveWindow = null, CancellationToken ct = default(CancellationToken), int retryAttempts = 3)
        {
            var binanceOrderSide = _mapper.Map<Binance.Net.Enums.OrderSide>(orderSide);
            var binanceOrderType = _mapper.Map<Binance.Net.Enums.SpotOrderType>(orderType);
            var binanceCancelReplaceMode = _mapper.Map<Binance.Net.Enums.CancelReplaceMode>(cancelReplaceMode);
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var result = await ExecuteWithRetry(() => client.SpotApi.Trading.ReplaceOrderAsync(symbol:symbol, side: binanceOrderSide, type: binanceOrderType, cancelOrderId: cancelOrderId, cancelReplaceMode: binanceCancelReplaceMode), retryAttempts);
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

        public Task<IEnumerable<BinanceOrderBase>> CancelAllOrdersAsync(string symbol, int retryAttempts = 3)
        {
            // todo 
            throw new NotImplementedException();
        }
    }
}
