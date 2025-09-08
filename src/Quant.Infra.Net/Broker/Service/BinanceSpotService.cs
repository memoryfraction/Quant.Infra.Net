using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
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
    public class BinanceSpotService : IBinanceSpotService
    {
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly string _apiKey, _apiSecret;

        public ExchangeEnvironment ExchangeEnvironment { get; set; } = ExchangeEnvironment.Testnet;

        public BinanceSpotService(IConfiguration configuration)
        {
            _apiKey = configuration["Exchange:ApiKey"];
            _apiSecret = configuration["Exchange:ApiSecret"];
            ExchangeEnvironment = (ExchangeEnvironment)Enum.Parse(typeof(ExchangeEnvironment), configuration["Exchange:Environment"]?.ToString() ?? "Testnet");

            // 优化重试策略，针对网络相关异常
            _retryPolicy = Policy
                .Handle<Exception>(ex => ex.Message.Contains("EOF") || ex is System.Net.Http.HttpRequestException || ex is System.IO.IOException)
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, retryCount, context) =>
                    {
                        UtilityService.LogAndWriteLine(UtilityService.GenerateMessage($"Retry {retryCount} due to {exception.Message}"));
                    });

            // 支持TLS1.2和TLS1.3
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
        }

        private BinanceRestClient InitializeBinanceClient()
        {
            BinanceRestClient.SetDefaultOptions(options =>
            {
                // 显式设置测试网或主网环境
                options.Environment = ExchangeEnvironment == ExchangeEnvironment.Testnet ? BinanceEnvironment.Testnet : BinanceEnvironment.Live;
                options.RequestTimeout = TimeSpan.FromSeconds(30); // 设置超时时间为30秒
                // 仅在密钥有效时设置ApiCredentials
                if (!string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_apiSecret))
                    options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
            });
            return new BinanceRestClient();
        }

        public async Task<IEnumerable<string>> GetSpotSymbolsAsync()
        {
            var symbols = new List<string>();
            try
            {
                using var client = InitializeBinanceClient();
                var exchangeInfo = await _retryPolicy.ExecuteAsync(() => client.SpotApi.ExchangeData.GetExchangeInfoAsync());
                if (exchangeInfo.Success)
                {
                    symbols = exchangeInfo.Data.Symbols.Select(s => s.Name).ToList();
                    UtilityService.LogAndWriteLine(UtilityService.GenerateMessage($"Successfully fetched {symbols.Count} symbols."));
                }
                else
                {
                    UtilityService.LogAndWriteLine(UtilityService.GenerateMessage($"Error fetching exchange info: {exchangeInfo.Error?.Message}"));
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error in GetSpotSymbolsAsync: {ex.Message}, InnerException: {ex.InnerException?.Message}";
                UtilityService.LogAndWriteLine(UtilityService.GenerateMessage(msg));
                throw;
            }
            return symbols;
        }

        public async Task<IEnumerable<string>> GetAllSpotSymbolsEndingWithUSDTAsync()
        {
            var symbols = await GetSpotSymbolsAsync();
            var usdtSymbols = symbols.Where(s => s.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)).ToList();
            UtilityService.LogAndWriteLine(UtilityService.GenerateMessage($"Fetched {usdtSymbols.Count} USDT symbols."));
            return usdtSymbols;
        }

        public async Task<Ohlcvs> GetOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            var ohlcvs = new Ohlcvs
            {
                Symbol = symbol,
                ResolutionLevel = resolutionLevel,
                StartDateTimeUtc = startDt,
                EndDateTimeUtc = endDt
            };

            using var client = InitializeBinanceClient();
            var klineInterval = UtilityService.ConvertToKlineInterval(resolutionLevel);

            DateTime currentStart = startDt;
            const int maxRecords = 1000;

            while (currentStart < endDt)
            {
                var klineResult = await _retryPolicy.ExecuteAsync(() => client.SpotApi.ExchangeData.GetKlinesAsync(symbol, klineInterval, currentStart, endDt, maxRecords));
                if (!klineResult.Success)
                {
                    UtilityService.LogAndWriteLine(UtilityService.GenerateMessage($"Error fetching data: {klineResult.Error?.Message}"));
                    break;
                }

                foreach (var kline in klineResult.Data)
                {
                    ohlcvs.OhlcvSet.Add(new Ohlcv
                    {
                        Symbol = symbol,
                        OpenDateTime = kline.OpenTime,
                        CloseDateTime = kline.CloseTime,
                        Open = kline.OpenPrice,
                        High = kline.HighPrice,
                        Low = kline.LowPrice,
                        Close = kline.ClosePrice,
                        Volume = kline.Volume,
                        AdjustedClose = kline.ClosePrice
                    });
                }

                currentStart = klineResult.Data.Last().CloseTime.AddMilliseconds(1);
                if (currentStart >= endDt) break;
            }

            return ohlcvs;
        }

        public async Task<bool> HasSpotPositionAsync(string symbol)
        {
            using var client = InitializeBinanceClient();
            var accountInfo = await ExecuteWithRetryAsync(() => client.SpotApi.Account.GetAccountInfoAsync());
            if (!accountInfo.Success)
                throw new Exception($"Failed to get account info: {accountInfo.Error?.Message}");

            var balance = accountInfo.Data.Balances.FirstOrDefault(b => b.Asset.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            return balance != null && balance.Total > 0.0001m;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
        {
            return await _retryPolicy.ExecuteAsync(action);
        }
    }
}