using Binance.Net.Clients;
using Binance.Net.Objects;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Quant.Infra.Net.Broker.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Service
{
    public class BinanceUsdFutureService : IBinanceUsdFutureService
    {
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly string _apiKey, _apiSecret;

        public BinanceUsdFutureService(IConfiguration configuration)
        {
            _apiKey = configuration["LiveTradingSettings:ApiKey"];
            _apiSecret = configuration["LiveTradingSettings:ApiSecret"];

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); // Exponential backoff
        }

        private BinanceRestClient InitializeBinanceRestClient()
        {
            BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
            });
            return new BinanceRestClient();
        }

        public async Task<decimal> GetusdFutureAccountBalanceAsync()
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            var response = await binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync();

            if (!response.Success)
            {
                throw new Exception("Failed to retrieve account balances.");
            }

            decimal totalUSDBasedBalance = response.Data.Sum(token => token.WalletBalance);
            return totalUSDBasedBalance;
        }

        public async Task<double> GetusdFutureUnrealizedProfitRateAsync(decimal lastOpenPortfolioMarketValue)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            var response = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync());

            if (!response.Success)
            {
                throw new Exception("Failed to retrieve account balances.");
            }

            decimal currentPortfolioMarketValue = response.Data.Sum(token => token.WalletBalance);

            if (lastOpenPortfolioMarketValue == 0)
            {
                throw new Exception("Last open portfolio market value cannot be zero.");
            }

            double unrealizedProfitRate = (double)((currentPortfolioMarketValue - lastOpenPortfolioMarketValue) / lastOpenPortfolioMarketValue);
            return unrealizedProfitRate;
        }

        public async Task usdFutureLiquidateAsync(string symbol)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            var response = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            if (!response.Success)
            {
                throw new Exception("Failed to retrieve position information.");
            }

            var position = response.Data.FirstOrDefault(p => p.Symbol == symbol);
            if (position == null || position.Quantity == 0)
            {
                throw new Exception("No open position found for the given symbol.");
            }

            var orderSide = position.Quantity > 0 ? Binance.Net.Enums.OrderSide.Sell : Binance.Net.Enums.OrderSide.Buy;
            var exitResponse = await ExecuteWithRetryAsync(() =>
                binanceRestClient.UsdFuturesApi.Trading.PlaceOrderAsync(
                    symbol: symbol,
                    side: orderSide,
                    type: Binance.Net.Enums.FuturesOrderType.Market,
                    quantity: Math.Abs(position.Quantity),
                    positionSide: orderSide == Binance.Net.Enums.OrderSide.Sell ? Binance.Net.Enums.PositionSide.Long : Binance.Net.Enums.PositionSide.Short
                )
            );

            if (!exitResponse.Success)
            {
                throw new Exception($"Failed to liquidate position for {symbol}: {exitResponse.Error?.Message}");
            }
        }

        public async Task SetUsdFutureHoldingsAsync(string symbol, double rate)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            var accountResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync());

            if (!accountResponse.Success)
            {
                throw new Exception("Failed to retrieve account balance.");
            }

            decimal usdtBalance = accountResponse.Data.First(b => b.Asset == "USDT").WalletBalance;

            var priceResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol));

            if (!priceResponse.Success)
            {
                throw new Exception($"Failed to retrieve the latest price for {symbol}.");
            }

            decimal latestPrice = priceResponse.Data.Price;

            var positionResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            if (!positionResponse.Success)
            {
                throw new Exception("Failed to retrieve position information.");
            }

            var position = positionResponse.Data.FirstOrDefault(p => p.Symbol == symbol);
            decimal currentPositionSize = position != null ? position.Quantity : 0m;

            decimal targetPositionSize = (usdtBalance * (decimal)rate) / latestPrice;
            decimal positionDifference = targetPositionSize - currentPositionSize;

            if (positionDifference != 0)
            {
                var orderSide = positionDifference > 0 ? Binance.Net.Enums.OrderSide.Buy : Binance.Net.Enums.OrderSide.Sell;
                decimal quantityToTrade = Math.Abs(positionDifference);

                var orderResponse = await ExecuteWithRetryAsync(() =>
                    binanceRestClient.UsdFuturesApi.Trading.PlaceOrderAsync(
                        symbol: symbol,
                        side: orderSide,
                        type: Binance.Net.Enums.FuturesOrderType.Market,
                        quantity: quantityToTrade,
                        positionSide: currentPositionSize >= 0 ? Binance.Net.Enums.PositionSide.Long : Binance.Net.Enums.PositionSide.Short
                    )
                );

                if (!orderResponse.Success)
                {
                    throw new Exception($"Failed to place market order for {symbol}: {orderResponse.Error?.Message}");
                }
            }
            else
            {
                Console.WriteLine("Position already at target size, no action required.");
            }
        }

        public async Task<bool> HasUsdFuturePositionAsync(string symbol)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            var positionResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            if (!positionResponse.Success)
            {
                throw new Exception("Failed to retrieve position information.");
            }

            var position = positionResponse.Data.FirstOrDefault(p => p.Symbol == symbol);
            return position?.Quantity > 0.000001m;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> apiCall)
        {
            return await _retryPolicy.ExecuteAsync(async () => await apiCall());
        }
    }
}
