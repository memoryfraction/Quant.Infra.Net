using Binance.Net;
using Binance.Net.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Service
{
    public class BinanceUsdFutureService : IBinanceUsdFutureService
    {
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly string _apiKey, _apiSecret;

        public ExchangeEnvironment ExchangeEnvironment { get; set; } = ExchangeEnvironment.Testnet;

        public BinanceUsdFutureService(IConfiguration configuration)
        {
            _apiKey = configuration["Exchange:ApiKey"];
            _apiSecret = configuration["Exchange:ApiSecret"];
            ExchangeEnvironment = (ExchangeEnvironment)Enum.Parse(typeof(ExchangeEnvironment), configuration["Exchange:Environment"].ToString());

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); // Exponential backoff
        }

        private BinanceRestClient InitializeBinanceRestClient()
        {
            BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
                // 启动测试网络
                if (ExchangeEnvironment == ExchangeEnvironment.Testnet)
                {
                    options.Environment = BinanceEnvironment.Testnet;
                }
                else if(ExchangeEnvironment == ExchangeEnvironment.Live)
                {
                    options.Environment = BinanceEnvironment.Live;
                }
            });
            return new BinanceRestClient();
        }

        public async Task<decimal> GetusdFutureAccountBalanceAsync()
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            UtilityService.LogAndConsole($"Requesting account balances at {DateTime.UtcNow}");

            var response = await binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync();

            UtilityService.LogAndConsole($"Received response at {DateTime.UtcNow}: Success = {response.Success}, Error = {response.Error?.Message}");

            if (!response.Success)
            {
                throw new Exception($"Failed to retrieve account balances. Error Code: {response.Error.Code}, Message: {response.Error.Message}");
            }

            decimal totalUSDBasedBalance = response.Data.Sum(token => token.WalletBalance);
            return totalUSDBasedBalance;
        }

        public async Task<double> GetusdFutureUnrealizedProfitRateAsync(decimal lastOpenPortfolioMarketValue)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            UtilityService.LogAndConsole($"Requesting account balances at {DateTime.UtcNow}");

            var response = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync());

            UtilityService.LogAndConsole($"Received response at {DateTime.UtcNow}: Success = {response.Success}, Error = {response.Error?.Message}");

            if (!response.Success)
            {
                throw new Exception($"Failed to retrieve account balances. Error Code: {response.Error.Code}, Message: {response.Error.Message}");
            }

            decimal currentPortfolioMarketValue = response.Data.Sum(token => token.WalletBalance);

            if (lastOpenPortfolioMarketValue == 0)
            {
                throw new ArgumentException("Last open portfolio market value cannot be zero.");
            }

            double unrealizedProfitRate = (double)((currentPortfolioMarketValue - lastOpenPortfolioMarketValue) / lastOpenPortfolioMarketValue);
            return unrealizedProfitRate;
        }

        public async Task usdFutureLiquidateAsync(string symbol)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            UtilityService.LogAndConsole($"Requesting position information for {symbol} at {DateTime.UtcNow}");

            var response = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            UtilityService.LogAndConsole($"Received position information at {DateTime.UtcNow}: Success = {response.Success}, Error = {response.Error?.Message}");

            if (!response.Success)
            {
                throw new Exception($"Failed to retrieve position information. Error Code: {response.Error.Code}, Message: {response.Error.Message}");
            }

            var position = response.Data.FirstOrDefault(p => p.Symbol == symbol);
            if (position == null || position.Quantity == 0)
            {
                throw new InvalidOperationException($"No open position found for the given symbol: {symbol}");
            }

            var orderSide = position.Quantity > 0 ? Binance.Net.Enums.OrderSide.Sell : Binance.Net.Enums.OrderSide.Buy;
            UtilityService.LogAndConsole($"Placing liquidation order for {symbol} at {DateTime.UtcNow}");

            var exitResponse = await ExecuteWithRetryAsync(() =>
                binanceRestClient.UsdFuturesApi.Trading.PlaceOrderAsync(
                    symbol: symbol,
                    side: orderSide,
                    type: Binance.Net.Enums.FuturesOrderType.Market,
                    quantity: Math.Abs(position.Quantity),
                    positionSide: orderSide == Binance.Net.Enums.OrderSide.Sell ? Binance.Net.Enums.PositionSide.Long : Binance.Net.Enums.PositionSide.Short
                )
            );

            UtilityService.LogAndConsole($"Liquidation order response at {DateTime.UtcNow}: Success = {exitResponse.Success}, Error = {exitResponse.Error?.Message}");

            if (!exitResponse.Success)
            {
                throw new Exception($"Failed to liquidate position for {symbol}. Error Code: {exitResponse.Error.Code}, Message: {exitResponse.Error.Message}");
            }
        }

        public async Task SetUsdFutureHoldingsAsync(string symbol, double rate)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            UtilityService.LogAndConsole($"Requesting account balance for {symbol} at {DateTime.UtcNow}");

            var accountResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync());

            UtilityService.LogAndConsole($"Received account balance response at {DateTime.UtcNow}: Success = {accountResponse.Success}, Error = {accountResponse.Error?.Message}");

            if (!accountResponse.Success)
            {
                throw new Exception($"Failed to retrieve account balance. Error Code: {accountResponse.Error.Code}, Message: {accountResponse.Error.Message}");
            }

            decimal usdtBalance = accountResponse.Data.First(b => b.Asset == "USDT").WalletBalance;

            var priceResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol));

            UtilityService.LogAndConsole($"Received latest price for {symbol} at {DateTime.UtcNow}: Success = {priceResponse.Success}, Error = {priceResponse.Error?.Message}");

            if (!priceResponse.Success)
            {
                throw new Exception($"Failed to retrieve the latest price for {symbol}. Error Code: {priceResponse.Error.Code}, Message: {priceResponse.Error.Message}");
            }

            decimal latestPrice = priceResponse.Data.Price;

            var positionResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            UtilityService.LogAndConsole($"Received position information for {symbol} at {DateTime.UtcNow}: Success = {positionResponse.Success}, Error = {positionResponse.Error?.Message}");

            if (!positionResponse.Success)
            {
                throw new Exception($"Failed to retrieve position information. Error Code: {positionResponse.Error.Code}, Message: {positionResponse.Error.Message}");
            }

            var position = positionResponse.Data.FirstOrDefault(p => p.Symbol == symbol);
            decimal currentPositionSize = position != null ? position.Quantity : 0m;

            decimal targetPositionSize = (usdtBalance * (decimal)rate) / latestPrice;
            decimal positionDifference = targetPositionSize - currentPositionSize;

            if (positionDifference != 0)
            {
                var orderSide = positionDifference > 0 ? Binance.Net.Enums.OrderSide.Buy : Binance.Net.Enums.OrderSide.Sell;
                decimal quantityToTrade = Math.Abs(positionDifference);

                UtilityService.LogAndConsole($"Placing order for {symbol} to adjust position size at {DateTime.UtcNow}");

                var orderResponse = await ExecuteWithRetryAsync(() =>
                    binanceRestClient.UsdFuturesApi.Trading.PlaceOrderAsync(
                        symbol: symbol,
                        side: orderSide,
                        type: Binance.Net.Enums.FuturesOrderType.Market,
                        quantity: quantityToTrade,
                        positionSide: currentPositionSize >= 0 ? Binance.Net.Enums.PositionSide.Long : Binance.Net.Enums.PositionSide.Short
                    )
                );

                UtilityService.LogAndConsole($"Order response for {symbol} at {DateTime.UtcNow}: Success = {orderResponse.Success}, Error = {orderResponse.Error?.Message}");

                if (!orderResponse.Success)
                {
                    throw new Exception($"Failed to place order for {symbol}. Error Code: {orderResponse.Error.Code}, Message: {orderResponse.Error.Message}");
                }
            }
        }

        public async Task<bool> HasUsdFuturePositionAsync(string symbol)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            UtilityService.LogAndConsole($"Checking if there is an open position for {symbol} at {DateTime.UtcNow}");

            var positionResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            UtilityService.LogAndConsole($"Position check response for {symbol} at {DateTime.UtcNow}: Success = {positionResponse.Success}, Error = {positionResponse.Error?.Message}");

            if (!positionResponse.Success)
            {
                throw new Exception($"Failed to retrieve position information. Error Code: {positionResponse.Error.Code}, Message: {positionResponse.Error.Message}");
            }

            var position = positionResponse.Data.FirstOrDefault(p => p.Symbol == symbol);
            return position != null && position.Quantity != 0;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
        {
            return await _retryPolicy.ExecuteAsync(action);
        }
    }
}
