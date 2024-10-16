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
using System.Collections.Generic;
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
            var msg = UtilityService.GenerateMessage("Requesting account balances");
            UtilityService.LogAndConsole(msg);

            var response = await binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync();

            msg = $"Received response: Success = {response.Success}";
            var errors = new List<string>() { response.Error?.Message };
            var message = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndConsole(message);

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

            var msg = "Requesting account balances";
            var message = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndConsole(message);

            var response = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync());

            msg = $"Received response: Success = {response.Success}";
            var errors = new List<string>() { response.Error?.Message };
            message = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndConsole(message);
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

            var msg = $"Requesting position information for {symbol}";
            var errors = new List<string>() {  };
            var message = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndConsole(message);

            var response = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            UtilityService.LogAndConsole($"Received position information at {DateTime.UtcNow}: Success = {response.Success}, Error = {response.Error?.Message}");
            msg = $"Received position information: Success = {response.Success}";
            errors = new List<string>() { response.Error?.Message };
            message = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndConsole(message);

            if (!response.Success)
            {
                throw new Exception($"Failed to retrieve position information. Error Code: {response.Error.Code}, Message: {response.Error.Message}");
            }

            var positions = response.Data.Where(x => x.Symbol == symbol).ToList();
            if (positions.Count() == 0 || positions.Select(x=>x.Quantity).Sum() == 0m)
            {
                msg = $"No open position found for the given symbol: {symbol}";
                msg = UtilityService.GenerateMessage(msg);
                UtilityService.LogAndConsole(msg);

                return;
            }

            WebCallResult<BinanceUsdFuturesOrder> exitResponse = null;
            var positivePosition = positions.Where(x => x.Quantity > 0).FirstOrDefault();            
            if (positivePosition != null) // 持有正向仓位，Sell以平仓
            {
                msg = $"Placing liquidation order for {symbol}";
                msg = UtilityService.GenerateMessage(msg);
                UtilityService.LogAndConsole(msg);

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
                msg = $"Placing liquidation order for {symbol}";
                msg = UtilityService.GenerateMessage(msg);
                UtilityService.LogAndConsole(msg);

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

            msg = $"Liquidation order response: Success = {exitResponse.Success}";
            errors = new List<string>() {  exitResponse.Error?.Message  };
            msg = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndConsole(msg);

            if (!exitResponse.Success)
            {
                throw new Exception($"Failed to liquidate position for {symbol}. Error Code: {exitResponse.Error.Code}, Message: {exitResponse.Error.Message}");
            }
        }

        public async Task SetUsdFutureHoldingsAsync(string symbol, double rate, PositionSide positionSide = PositionSide.Both)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            UtilityService.LogAndConsole($"Requesting account balance for {symbol} at {DateTime.UtcNow}");
            var msg = $"Requesting account balance for {symbol}";
            msg = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndConsole(msg);

            var accountResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync());

            msg = $"Received account balance response: Success = {accountResponse.Success}";
            var errors = new List<string>()  { accountResponse.Error?.Message  };
            msg = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndConsole(msg);

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

                msg = $"Placing order for {symbol} to adjust position size";
                errors = new List<string>() { };
                var message = UtilityService.GenerateMessage(msg, errors);
                UtilityService.LogAndConsole(message);


                var orderResponse = await ExecuteWithRetryAsync(() =>
                    binanceRestClient.UsdFuturesApi.Trading.PlaceOrderAsync(
                        symbol: symbol,
                        side: orderSide,
                        type: Binance.Net.Enums.FuturesOrderType.Market,
                        quantity: quantityToTrade,
                        positionSide: positionSide // LONG/SHORT是对冲模式， 多头开关都用LONG, 空头开关都用SHORT
                    )
                );

                msg = $"Order response for {symbol}: Success = {orderResponse.Success}";
                errors = new List<string>() { orderResponse.Error?.Message };
                msg = UtilityService.GenerateMessage(msg);
                UtilityService.LogAndConsole(msg);

                if (!orderResponse.Success)
                {
                    throw new Exception($"Failed to place order for {symbol}. Error Code: {orderResponse.Error.Code}, Message: {orderResponse.Error.Message}");
                }
            }
        }

        public async Task<bool> HasUsdFuturePositionAsync(string symbol)
        {
            using var binanceRestClient = InitializeBinanceRestClient();

            var msg = $"Checking if there is an open position for {symbol}";
            var errors = new List<string>() {  };
            msg = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndConsole(msg);

            var positionResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            msg = $"Position check response for {symbol}: Success = {positionResponse.Success}";
            errors = new List<string>() { positionResponse.Error?.Message };
            msg = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndConsole(msg);

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
