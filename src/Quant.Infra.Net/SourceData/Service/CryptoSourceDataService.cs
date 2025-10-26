using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Service
{
    public interface ICryptoSourceDataService
    {
        Task DownloadBinanceAllSpotAsync(DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);

        Task DownloadBinanceAllUsdFutureAsync(DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);

        Task DownloadBinanceSpotAsync(IEnumerable<string> symbols, DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);

        Task DownloadBinanceUsdFutureAsync(IEnumerable<string> symbols, DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);

        public Task<List<string>> GetTopMarketCapSymbolsFromCoinMarketCapAsync(string cmcApiKey, string cmcBaseUrl = "https://pro-api.coinmarketcap.com", int count = 50);

        public Task<List<string>> GetAllBinanceSpotSymbolsAsync();
        public Task<List<string>> GetAllBinanceUsdFutureSymbolsAsync();

    }

    public class CryptoSourceDataService : ICryptoSourceDataService
    {

        public async Task DownloadBinanceAllSpotAsync(DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour)
        {
            var symbols = new List<string>();
            //1 GET BINANCE SPOT SYMBOLS
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var exchangeInfo = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();
                symbols = exchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
                Console.WriteLine(symbols.Count);
            }

            //2 过滤出以稳定币为结尾的symbol否则没有意义
            // 找出symbols中仅存在于stableCoinSymbols的部分，大小写不敏感
            var filteredSymbols = new List<string>();
            filteredSymbols.AddRange(symbols.Where(x => x.ToLower().EndsWith("usdt")).Select(x => x).ToList());

            //3 下载
            if (string.IsNullOrEmpty(path))
                path = AppDomain.CurrentDomain.BaseDirectory + "\\data\\spot\\";
            if (!Directory.Exists(path))
                await Task.Run(() => Directory.CreateDirectory(path));

            var interval = klineInterval; // 时间间隔默认为1天
            foreach (var symbol in filteredSymbols)
            {
                Console.WriteLine($"Downloading: {symbol}.");
                var fileName = $"{symbol}.csv";
                var fullPathFileName = Path.Combine(path, fileName);

                await SaveSpotKlinesToCsv(symbol, interval, startDt, endDt, fullPathFileName);
            }
            Console.WriteLine($"All done!");
        }

        public async Task DownloadBinanceAllUsdFutureAsync(DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour)
        {
            //1 GET BINANCE UsdFuture symbols
            var symbols = new List<string>();
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var exchangeInfo = await client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
                symbols = exchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
                Console.WriteLine($"symbols.Count: {symbols.Count}");
            }

            //2 过滤出以稳定币为结尾的symbol否则没有意义
            var filteredSymbols = new List<string>();
            filteredSymbols.AddRange(symbols.Where(x => x.ToLower().EndsWith("usdt")).Select(x => x).ToList());

            //3 下载
            if (string.IsNullOrEmpty(path))
                path = AppDomain.CurrentDomain.BaseDirectory + "\\data\\UsdFuture\\";
            if (!Directory.Exists(path))
                await Task.Run(() => Directory.CreateDirectory(path));

            var interval = klineInterval; // 时间间隔默认为1天
            foreach (var symbol in filteredSymbols)
            {
                if (symbol == "BTCSTUSDT")
                    continue;
                Console.WriteLine($"Downloading: {symbol}.");
                var fileName = $"{symbol}.csv";
                var fullPathFileName = Path.Combine(path, fileName);
                try
                {
                    await SaveUsdFutureKlinesToCsv(symbol, interval, startDt, endDt, fullPathFileName);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Information($"Current utc datetime: + {DateTime.UtcNow}, error: {ex.Message}");
                    continue;
                }
            }
            Console.WriteLine($"All done!");
        }

        /// <summary>
        /// 获取CoinMarketCap 市值前count的symbols
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        /// <summary>
        /// 获取CoinMarketCap 市值前count的symbols
        /// </summary>
        /// <param name="cmcApiKey">X-CMC_PRO_API_KEY</param>
        /// <param name="cmcBaseUrl">默认 https://pro-api.coinmarketcap.com，也可传 https://sandbox-api.coinmarketcap.com</param>
        /// <param name="count">返回前 count 个 symbol</param>
        /// <returns></returns>
        public async Task<List<string>> GetTopMarketCapSymbolsFromCoinMarketCapAsync(
            string cmcApiKey,
            string cmcBaseUrl = "https://pro-api.coinmarketcap.com",
            int count = 50)
        {
            if (string.IsNullOrWhiteSpace(cmcApiKey))
                throw new ArgumentException("cmcApiKey 不能为空。", nameof(cmcApiKey));
            if (string.IsNullOrWhiteSpace(cmcBaseUrl))
                throw new ArgumentException("cmcBaseUrl 不能为空。", nameof(cmcBaseUrl));
            if (!Uri.TryCreate(cmcBaseUrl, UriKind.Absolute, out var baseUri))
                throw new ArgumentException("cmcBaseUrl 不是有效的绝对 URL。", nameof(cmcBaseUrl));
            if (count <= 0) return new List<string>();

            var limit = Math.Min(count, 5000); // CMC 单次上限足够
            var url = new Uri(baseUri, $"/v1/cryptocurrency/listings/latest?limit={limit}&convert=USD&sort=market_cap&sort_dir=desc");

            using var http = new HttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-CMC_PRO_API_KEY", cmcApiKey);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                var preview = body.Length > 300 ? body[..300] + "..." : body;
                throw new HttpRequestException($"CoinMarketCap HTTP {(int)resp.StatusCode} - {resp.ReasonPhrase}; Body Preview: {preview}");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync();
            var payload = await JsonSerializer.DeserializeAsync<CmcListingsResponse>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (payload?.Status is null)
                throw new InvalidOperationException("CoinMarketCap: 响应缺少 status 字段。");

            if (payload.Status.ErrorCode != 0)
                throw new InvalidOperationException($"CoinMarketCap 错误 {payload.Status.ErrorCode}: {payload.Status.ErrorMessage ?? "Unknown error"}");

            return payload.Data?
                .Select(d => d.Symbol)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(count)
                .ToList()
                ?? new List<string>();
        }


        #region private functions

        /// <summary>
        /// 从Binance下载数据， 存到制定的(csv)文件
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="interval"></param>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="fullPathFileName"></param>
        /// <returns></returns>
        private async Task SaveSpotKlinesToCsv(string symbol, Binance.Net.Enums.KlineInterval interval, DateTime startDt, DateTime endDt, string fullPathFileName)
        {
            if (endDt > DateTime.Now)
                throw new ArgumentOutOfRangeException();

            // 确保路径存在
            var directory = Path.GetDirectoryName(fullPathFileName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var ohlcvs = new HashSet<Ohlcv>();
            using (var client = new BinanceRestClient())
            {
                var lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                var paramStartDt = startDt;
                while (lastDtInOhlcvs < endDt)
                {
                    // 每次调用默认只能获取500条数据;
                    if (lastDtInOhlcvs != default(DateTime)) // 此时ohlcvs有值
                    {
                        paramStartDt = lastDtInOhlcvs;
                    }

                    // 获取历史K线数据
                    var klinesResult = await client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, paramStartDt, endDt);
                    if (klinesResult.Success)
                    {
                        if (klinesResult.Data.Count() == 0) // 如果取不到数据，跳过该币种
                            return;
                        ohlcvs = UpsertOhlcvs(klinesResult.Data, ohlcvs, startDt, endDt);
                    }
                    else
                    {
                        UtilityService.LogAndWriteLine($"Symbol: {symbol}, error message: {klinesResult.Error.Message}");
                        return;
                    }
                    lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                }

                // Save ohlcvs to file: {fullPathFileName} using csvHelper
                lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                var firstDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).FirstOrDefault();
                if (firstDtInOhlcvs.Date != startDt)
                {
                    Console.WriteLine($"firstDtInOhlcvs:{firstDtInOhlcvs} does not match with startDt:{startDt}");
                    return;
                }
                if (lastDtInOhlcvs.Date != endDt)
                {
                    Console.WriteLine($"lastDtInOhlcvs:{firstDtInOhlcvs} does not match with endDt:{endDt}");
                    return;
                }

                if (File.Exists(fullPathFileName))
                    File.Delete(fullPathFileName);
                using (var writer = new StreamWriter(fullPathFileName))
                {
                    writer.WriteLine("DateTime,Open,High,Low,Close,Volume");
                    foreach (var ohlcv in ohlcvs)
                    {
                        writer.WriteLine($"{ohlcv.OpenDateTime},{ohlcv.Open},{ohlcv.High},{ohlcv.Low},{ohlcv.Close},{ohlcv.Volume}");
                    }
                }
                Console.WriteLine($"Klines data saved successfully for {symbol}.");
            }
        }

        private async Task SaveUsdFutureKlinesToCsv(string symbol, Binance.Net.Enums.KlineInterval interval, DateTime startDt, DateTime endDt, string fullPathFileName)
        {
            if (endDt > DateTime.Now)
                throw new ArgumentOutOfRangeException();

            // 确保路径存在
            var directory = Path.GetDirectoryName(fullPathFileName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var ohlcvs = new HashSet<Ohlcv>();
            using (var client = new BinanceRestClient())
            {
                var lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                var paramStartDt = startDt;
                while (lastDtInOhlcvs < endDt)
                {
                    // 每次调用默认只能获取500条数据;
                    if (lastDtInOhlcvs != default(DateTime)) // 此时ohlcvs有值
                    {
                        paramStartDt = lastDtInOhlcvs;
                    }

                    // 获取历史K线数据
                    var klinesResult = await client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, interval, paramStartDt, endDt);
                    if (klinesResult.Success)
                    {
                        if (klinesResult.Data.Count() == 0) // 如果取不到数据，跳过该币种
                            return;
                        ohlcvs = UpsertOhlcvs(klinesResult.Data, ohlcvs, startDt, endDt);
                    }
                    else
                    {
                        throw new Exception($"Error fetching klines for {symbol}: {klinesResult.Error.Message}");
                    }
                    lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                }

                // Save ohlcvs to file: {fullPathFileName} using csvHelper
                lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                var firstDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).FirstOrDefault();
                if (firstDtInOhlcvs.Date != startDt)
                {
                    Console.WriteLine($"firstDtInOhlcvs:{firstDtInOhlcvs} does not match with startDt:{startDt}");
                    return;
                }
                if (lastDtInOhlcvs.Date != endDt)
                {
                    Console.WriteLine($"lastDtInOhlcvs:{firstDtInOhlcvs} does not match with endDt:{endDt}");
                    return;
                }

                if (File.Exists(fullPathFileName))
                    File.Delete(fullPathFileName);
                using (var writer = new StreamWriter(fullPathFileName))
                {
                    writer.WriteLine("DateTime,Open,High,Low,Close,Volume");
                    foreach (var ohlcv in ohlcvs)
                    {
                        writer.WriteLine($"{ohlcv.OpenDateTime},{ohlcv.Open},{ohlcv.High},{ohlcv.Low},{ohlcv.Close},{ohlcv.Volume}");
                    }
                }
                Console.WriteLine($"Klines data saved successfully for {symbol}.");
            }
        }

        private HashSet<Ohlcv> UpsertOhlcvs(IEnumerable<IBinanceKline> klines, HashSet<Ohlcv> ohlcvs, DateTime startDt, DateTime endDt)
        {
            foreach (var kline in klines)
            {
                if (kline.CloseTime < startDt)
                    continue;

                var ohlcv = new Ohlcv()
                {
                    OpenDateTime = kline.CloseTime,
                    Open = kline.OpenPrice,
                    High = kline.HighPrice,
                    Low = kline.LowPrice,
                    Close = kline.ClosePrice,
                    Volume = kline.Volume
                };

                ohlcvs.Add(ohlcv);
                if (kline.CloseTime > endDt)
                    break;
            }
            return ohlcvs;
        }

        public async Task DownloadBinanceSpotAsync(
            IEnumerable<string> symbols,
            DateTime startDt,
            DateTime endDt,
            string path = "",
            KlineInterval klineInterval = KlineInterval.OneHour)
        {
            // 1. 验证和准备
            var symbolList = symbols?.ToList();
            if (symbolList is null || symbolList.Count == 0)
            {
                Console.WriteLine("No symbols provided for spot download. Aborting.");
                return;
            }

            // 2. 路径处理
            if (string.IsNullOrWhiteSpace(path))
            {
                // 默认路径：BaseDirectory/data/spot/
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "spot");
            }
            Directory.CreateDirectory(path); // 幂等，若已存在则无操作

            var interval = klineInterval;

            Console.WriteLine($"Starting Spot data download for {symbolList.Count} symbols from {startDt:yyyy-MM-dd} to {endDt:yyyy-MM-dd} at {interval} interval.");

            // 3. 顺序下载（普通 foreach）
            var binanceSymbols = await GetAllBinanceSpotSymbolsAsync();
            foreach (var symbol in symbolList)
            {
                try
                {
                    Console.WriteLine($"Downloading spot: {symbol}...");
                    var fileName = $"{symbol}.csv";
                    var fullPathFileName = Path.Combine(path, fileName);

                    await SaveSpotKlinesToCsv(symbol, interval, startDt, endDt, fullPathFileName);

                    Console.WriteLine($"Successfully saved spot: {symbol}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Current utc datetime: {DateTime.UtcNow}. Error downloading spot data for {symbol}: {ex.Message}");
                    // 继续下一个 symbol
                }
            }

            Console.WriteLine("Spot data download for all specified symbols done!");
        }

        public async Task DownloadBinanceUsdFutureAsync(
            IEnumerable<string> symbols,
            DateTime startDt,
            DateTime endDt,
            string path = "",
            KlineInterval klineInterval = KlineInterval.OneHour)
        {
            // 1. 验证和准备
            var symbolList = symbols?.ToList();
            if (symbolList is null || symbolList.Count == 0)
            {
                Console.WriteLine("No symbols provided for USD Future download. Aborting.");
                return;
            }

            // 2. 路径处理
            if (string.IsNullOrWhiteSpace(path))
            {
                // 默认路径：BaseDirectory/data/UsdFuture/
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "UsdFuture");
            }
            Directory.CreateDirectory(path); // 幂等

            var interval = klineInterval;

            Console.WriteLine($"Starting USD Future data download for {symbolList.Count} symbols from {startDt:yyyy-MM-dd} to {endDt:yyyy-MM-dd} at {interval} interval.");

            // 3. 顺序下载
            foreach (var symbol in symbolList)
            {
                try
                {
                    // 如需跳过特殊合约，保留你的逻辑
                    if (symbol.Equals("BTCSTUSDT", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Console.WriteLine($"Downloading USD Future: {symbol}...");
                    var fileName = $"{symbol}.csv";
                    var fullPathFileName = Path.Combine(path, fileName);

                    await SaveUsdFutureKlinesToCsv(symbol, interval, startDt, endDt, fullPathFileName);

                    Console.WriteLine($"Successfully saved USD Future: {symbol}");
                }
                catch (Exception ex)
                {
                    UtilityService.LogAndWriteLine($"Current utc datetime: {DateTime.UtcNow}. Error downloading USD Future data for {symbol}: {ex.Message}");
                    Thread.Sleep(200);
                    continue;
                }
            }

            Console.WriteLine("USD Future data download are done!");
        }


        public async Task<List<string>> GetAllBinanceSpotSymbolsAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var exchangeInfo = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();
                var symbols = exchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
                return symbols;
            }
        }


        public async Task<List<string>> GetAllBinanceUsdFutureSymbolsAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var exchangeInfo = await client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
                var symbols = exchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
                return symbols;
            }
        }


        #endregion
    }
}