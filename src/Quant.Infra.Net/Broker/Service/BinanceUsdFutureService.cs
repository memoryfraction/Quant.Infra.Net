using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Configuration;
using MySqlX.XDevAPI;
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

        public async Task LiquidateUsdFutureAsync(string symbol)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            UtilityService.LogAndConsole($"Requesting position information for {symbol} at {DateTime.UtcNow}");

            var response = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            UtilityService.LogAndConsole($"Received position information at {DateTime.UtcNow}: Success = {response.Success}, Error = {response.Error?.Message}");

            if (!response.Success)
            {
                throw new Exception($"Failed to retrieve position information. Error Code: {response.Error.Code}, Message: {response.Error.Message}");
            }

            var positions = response.Data.Where(x => x.Symbol == symbol).ToList();
            if (positions.Count() == 0 || positions.Select(x=>x.Quantity).Sum() == 0m)
            {
                var msg = $"No open position found for the given symbol: {symbol}";
                UtilityService.LogAndConsole(msg);
                return;
            }

            WebCallResult<BinanceUsdFuturesOrder> exitResponse = null;
            var positivePosition = positions.Where(x => x.Quantity > 0).FirstOrDefault();            
            if (positivePosition != null) // 持有正向仓位，Sell以平仓
            {
                UtilityService.LogAndConsole($"Placing liquidation order for {symbol} at {DateTime.UtcNow}");

                exitResponse = await ExecuteWithRetryAsync(() =>
                    binanceRestClient.UsdFuturesApi.Trading.PlaceOrderAsync(
                        symbol: symbol,
                        side: OrderSide.Sell,
                        type: Binance.Net.Enums.FuturesOrderType.Market,
                        quantity: Math.Abs(positivePosition.Quantity),
                        positionSide: positivePosition.PositionSide
                    )
                );
            }

            var negativePosition = positions.Where(x => x.Quantity < 0).FirstOrDefault();
            if (negativePosition != null) 
            {
                UtilityService.LogAndConsole($"Placing liquidation order for {symbol} at {DateTime.UtcNow}");
                exitResponse = await ExecuteWithRetryAsync(() =>
                    binanceRestClient.UsdFuturesApi.Trading.PlaceOrderAsync(
                        symbol: symbol,
                        side: OrderSide.Buy,
                        type: Binance.Net.Enums.FuturesOrderType.Market,
                        quantity: Math.Abs(negativePosition.Quantity),
                        positionSide: negativePosition.PositionSide
                    )
                );
            }

            UtilityService.LogAndConsole($"Liquidation order response at {DateTime.UtcNow}: Success = {exitResponse.Success}, Error = {exitResponse.Error?.Message}");

            if (!exitResponse.Success)
            {
                throw new Exception($"Failed to liquidate position for {symbol}. Error Code: {exitResponse.Error.Code}, Message: {exitResponse.Error.Message}");
            }
        }

        public async Task SetUsdFutureHoldingsAsync(string symbol, double rate, PositionSide positionSide = PositionSide.Both)
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

            var exchangeInfo = await binanceRestClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
            var symbolInfo = exchangeInfo.Data.Symbols.Single(s => s.Name == symbol);
            var pricePrecision = symbolInfo.PricePrecision;
            var quantityPrecision = symbolInfo.QuantityPrecision;
            positionDifference = Math.Round(positionDifference, quantityPrecision);


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
                        positionSide: positionSide // LONG/SHORT是对冲模式， 多头开关都用LONG, 空头开关都用SHORT
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

            var positions = positionResponse.Data.Where(x => x.Symbol == symbol).ToList();

            return positions.Any() && positions.Any(x=>x.Quantity>0.0001m);
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
        {
            return await _retryPolicy.ExecuteAsync(action);
        }

        public async Task ShowPositionModeAsync()
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            var positionMode = await binanceRestClient.UsdFuturesApi.Account.GetPositionModeAsync();
            if (positionMode.Data.IsHedgeMode)
            {
                Console.WriteLine("Hedge mode is enabled.");
            }
            else
            {
                Console.WriteLine("One-way mode is enabled. Unable to differentiate between long and short positions.");
            }

        }

        public async Task SetPositionModeAsync(bool isDualPositionSide = true)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            var response = await binanceRestClient.UsdFuturesApi.Account.ModifyPositionModeAsync(isDualPositionSide);
            if (response.Success)
            {
                Console.WriteLine(isDualPositionSide ? "Hedge mode has been set." : "One-way mode has been set.");
            }
            else
            {
                Console.WriteLine("Failed to set position mode: " + response.Error?.Message);
            }
        }
    }
}
