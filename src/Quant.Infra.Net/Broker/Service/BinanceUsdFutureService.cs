using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Model;
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
                if(string.IsNullOrEmpty(_apiKey) == false && string.IsNullOrEmpty(_apiSecret) == false)
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
            UtilityService.LogAndWriteLine(msg);

            var response = await binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync();
         
            if (!response.Success)
            {
                throw new Exception($"Failed to retrieve account balances. Error Code: {response.Error.Code}, Message: {response.Error.Message}");
            }

            decimal totalUSDBasedBalance = response.Data.Sum(token => token.WalletBalance);

            msg = $"Received response: Success = {response.Success}. Balance: {totalUSDBasedBalance} USD";
            var errors = new List<string>() { response.Error?.Message };
            var message = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndWriteLine(message);

            return totalUSDBasedBalance;
        }

        public async Task<double> GetusdFutureUnrealizedProfitRateAsync()
        {
            using var binanceRestClient = InitializeBinanceRestClient();

            var msg = "Requesting account balances";
            var message = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndWriteLine(message);

            var positionInformation = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());
            if (!positionInformation.Success) {
                throw new Exception($"Failed to get account positions. Error Code: {positionInformation.Error.Code}, Message: {positionInformation.Error.Message}.");
            }
            var holdingPositions = positionInformation.Data.Where(x => x.Quantity != 0m).Select(x => x).ToList();
            var totalCostBase = 0m;
            var totalCurrentMarketValue = 0m;
            foreach (var holdingPosition in holdingPositions)
            {
                totalCostBase = holdingPosition.EntryPrice * holdingPosition.Quantity;
                totalCurrentMarketValue = holdingPosition.MarkPrice * holdingPosition.Quantity;
            }
            return Convert.ToDouble((totalCurrentMarketValue - totalCostBase) / totalCostBase);
        }

        public async Task LiquidateUsdFutureAsync(string symbol)
        {
            using var binanceRestClient = InitializeBinanceRestClient();

            var msg = $"Requesting position information for {symbol}";
            var errors = new List<string>() {  };
            var message = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndWriteLine(message);

            var response = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

           

            if (!response.Success)
            {
                throw new Exception($"Failed to retrieve position information. Error Code: {response.Error.Code}, Message: {response.Error.Message}");
            }

            var positions = response.Data.Where(x => x.Symbol == symbol).ToList();
            if (positions.Count() == 0 || positions.Select(x=>x.Quantity).Sum() == 0m)
            {
                msg = $"No open position found for the given symbol: {symbol}";
                msg = UtilityService.GenerateMessage(msg);
                UtilityService.LogAndWriteLine(msg);

                return;
            }

            WebCallResult<BinanceUsdFuturesOrder> exitResponse = null;
            var positivePosition = positions.Where(x => x.Quantity > 0).FirstOrDefault();
            var negativePosition = positions.Where(x => x.Quantity < 0).FirstOrDefault();

            UtilityService.LogAndWriteLine($"Received positions information: Success = {response.Success}, " +
                $"Positive Position Symbol:{positivePosition.Symbol}, quantity: {positivePosition.Quantity}" +
                $"Negative Position Symbol:{negativePosition.Symbol}, quantity: {negativePosition.Quantity}" +
                $", Error = {response.Error?.Message}");
            msg = $"Received position information: Success = {response.Success}";
            errors = new List<string>() { response.Error?.Message };
            message = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndWriteLine(message);

            if (positivePosition != null) // 持有正向仓位，Sell以平仓
            {
                msg = $"Placing liquidation order for {symbol}, positivePosition quantity: {positivePosition}";
                msg = UtilityService.GenerateMessage(msg);
                UtilityService.LogAndWriteLine(msg);

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

            if (negativePosition != null) 
            {
                msg = $"Placing liquidation order for {symbol}, negativePosition quantity: {negativePosition}";
                msg = UtilityService.GenerateMessage(msg);
                UtilityService.LogAndWriteLine(msg);

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
            UtilityService.LogAndWriteLine(msg);

            if (!exitResponse.Success)
            {
                throw new Exception($"Failed to liquidate position for {symbol}. Error Code: {exitResponse.Error.Code}, Message: {exitResponse.Error.Message}");
            }
        }


        /// <summary>
        /// 调整持仓为目标百分比; 
        /// 需要注意：usdFuture 持仓有Long, Short两种，需要区别对待;
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="rate"></param>
        /// <param name="positionSide"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task SetUsdFutureHoldingsAsync(string symbol, double rate, PositionSide positionSide = PositionSide.Both)
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            UtilityService.LogAndWriteLine($"Requesting account balance for {symbol} at {DateTime.UtcNow}");
            var msg = $"Requesting account balance for {symbol}";
            msg = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndWriteLine(msg);

            var accountResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetBalancesAsync());

            if (!accountResponse.Success)
            {
                throw new Exception($"Failed to retrieve account balance. Error Code: {accountResponse.Error.Code}, Message: {accountResponse.Error.Message}");
            }

            decimal usdtBalance = accountResponse.Data.First(b => b.Asset == "USDT").WalletBalance;

            msg = $"Received account balance response: Success = {accountResponse.Success}. usdtBalancne: {usdtBalance}";
            var errors = new List<string>() { accountResponse.Error?.Message };
            msg = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndWriteLine(msg);

            var priceResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol));


            if (!priceResponse.Success)
            {
                throw new Exception($"Failed to retrieve the latest price for {symbol}. Error Code: {priceResponse.Error.Code}, Message: {priceResponse.Error.Message}");
            }

            decimal latestPrice = priceResponse.Data.Price;
            decimal targetPositionSize = (usdtBalance * (decimal)rate) / latestPrice;

            var positionResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            if (!positionResponse.Success)
            {
                throw new Exception($"Failed to retrieve position information. Error Code: {positionResponse.Error.Code}, Message: {positionResponse.Error.Message}");
            }

            var positions = positionResponse.Data.Where(x => x.Symbol == symbol);
            var positivePosition = positions.Where(x => x.PositionSide == PositionSide.Long).FirstOrDefault();
            var negativePosition = positions.Where(x => x.PositionSide == PositionSide.Short).FirstOrDefault();

            msg = $"Received position information for {symbol}: Success = {positionResponse.Success}. Symbol: {symbol} , positivePosition:{positivePosition.Quantity} , negativePosition: {negativePosition.Quantity} ";
            errors = new List<string> { positionResponse.Error?.Message };
            msg = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndWriteLine(msg);

            var exchangeInfo = await binanceRestClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
            var symbolInfo = exchangeInfo.Data.Symbols.Single(s => s.Name == symbol);
            var pricePrecision = symbolInfo.PricePrecision;
            var quantityPrecision = symbolInfo.QuantityPrecision;


            WebCallResult<BinanceUsdFuturesOrder> orderResponse = null;
            decimal positionDifference = 0m;
            if (positionSide == PositionSide.Long) // 如果是做多持仓方向
            {
                // 使用positivePosition计算difference
                positionDifference = targetPositionSize - positivePosition.Quantity;
            }
            else if (positionSide == PositionSide.Short)
            {
                // 使用negativePosition计算difference
                positionDifference = targetPositionSize - negativePosition.Quantity;
            }
            else if (positionSide == PositionSide.Both)
            {
                // 使用negativePosition计算difference
                positionDifference = targetPositionSize - positivePosition.Quantity;
            }
            else
            {
                throw new ArgumentOutOfRangeException("not supported position side.");
            }

            positionDifference = Math.Round(positionDifference, quantityPrecision);

            // place market buy order 
            var orderSide = positionDifference > 0 ? Binance.Net.Enums.OrderSide.Buy : Binance.Net.Enums.OrderSide.Sell;
            decimal quantityToTrade = Math.Abs(positionDifference);

            // place market buy order 
            msg = $"Placing order for {symbol} to adjust position size";
            errors = new List<string>() { };
            var message = UtilityService.GenerateMessage(msg, errors);
            UtilityService.LogAndWriteLine(message);

            orderResponse = await ExecuteWithRetryAsync(() =>
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
            UtilityService.LogAndWriteLine(msg);

            if (!orderResponse.Success)
            {
                throw new Exception($"Failed to place order for {symbol}. Error Code: {orderResponse.Error.Code}, Message: {orderResponse.Error.Message}");
            }

        }

        public async Task<bool> HasUsdFuturePositionAsync(string symbol)
        {
            using var binanceRestClient = InitializeBinanceRestClient();

            var msg = $"Checking if there is an open position for {symbol}";
            var errors = new List<string>() {  };
            msg = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndWriteLine(msg);

            var positionResponse = await ExecuteWithRetryAsync(() => binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync());

            msg = $"Position check response for {symbol}: Success = {positionResponse.Success}";
            errors = new List<string>() { positionResponse.Error?.Message };
            msg = UtilityService.GenerateMessage(msg);
            UtilityService.LogAndWriteLine(msg);

            if (!positionResponse.Success)
            {
                throw new Exception($"Failed to retrieve position information. Error Code: {positionResponse.Error.Code}, Message: {positionResponse.Error.Message}");
            }

            var positions = positionResponse.Data.Where(x => x.Symbol == symbol).ToList();

            return positions.Any() && positions.Any(x=> Math.Abs(x.Quantity) > 0.0001m);
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

        public async Task<IEnumerable<BinancePositionDetailsUsdt>> GetHoldingPositionAsync()
        {
            using var binanceRestClient = InitializeBinanceRestClient();
            var position = await binanceRestClient.UsdFuturesApi.Account.GetPositionInformationAsync();
            var holdingPositions = position.Data.Where(x => x.Quantity != 0).Select(x => x);
            return holdingPositions;
        }

        public async Task<IEnumerable<string>> GetUsdFutureSymbolsAsync()
        {
            var symbols = new List<string>();
            try
            {
                using var binanceRestClient = InitializeBinanceRestClient();
                var exchangeInfo = await binanceRestClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
                symbols = exchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
            }
            catch (Exception ex)
            {
                var msg = $"Error in GetUsdFutureSymbolsAsync: {ex.Message}";
                var message = UtilityService.GenerateMessage(msg);
                UtilityService.LogAndWriteLine(message);
            }
            return symbols;
        }


        /// <summary>
        /// 获取OHLCVs数据
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<Ohlcvs> GetOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            var ohlcvs = new Ohlcvs
            {
                Symbol = symbol,
                ResolutionLevel = resolutionLevel,
                StartDateTimeUtc = startDt,
                EndDateTimeUtc = endDt
            };
            using var binanceRestClient = InitializeBinanceRestClient();
            var klineInterval = UtilityService.ConvertToKlineInterval(resolutionLevel);

            DateTime currentStart = startDt;
            const int maxRecords = 1000; // Binance's max record limit

            // Loop to fetch data until reaching the end date
            while (currentStart < endDt)
            {
                // Fetch Kline data
                var klineResult = await binanceRestClient.UsdFuturesApi.ExchangeData
                    .GetKlinesAsync(symbol, klineInterval, currentStart, endDt, maxRecords);

                if (!klineResult.Success)
                {
                    Console.WriteLine($"Error fetching data: {klineResult.Error}");
                    break;
                }

                // Add each Kline entry to OhlcvSet
                foreach (var kline in klineResult.Data)
                {
                    var ohlcv = new Ohlcv
                    {
                        Symbol = symbol,
                        OpenDateTime = kline.OpenTime,
                        CloseDateTime = kline.CloseTime,
                        Open = kline.OpenPrice,
                        High = kline.HighPrice,
                        Low = kline.LowPrice,
                        Close = kline.ClosePrice,
                        Volume = kline.Volume,
                        AdjustedClose = kline.ClosePrice // Assuming AdjustedClose is same as Close for simplicity
                    };

                    ohlcvs.OhlcvSet.Add(ohlcv);
                }

                // Update the start time for the next call to the last close time of this batch
                currentStart = klineResult.Data.Last().CloseTime.AddMilliseconds(1);

                // Exit if we've fetched all the data up to end date
                if (currentStart >= endDt) break;
            }

            return ohlcvs;
        }
    }
}
