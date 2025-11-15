using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Model;
using Serilog.Events;
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
        Task DownloadBinanceSpotAsync(IEnumerable<string> symbols, DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);

        Task DownloadBinanceUsdFutureAsync(IEnumerable<string> symbols, DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);

        public Task<List<string>> GetTopMarketCapSymbolsFromCoinMarketCapAsync(string cmcApiKey, string cmcBaseUrl = "https://pro-api.coinmarketcap.com", int count = 50);

        public Task<List<string>> GetAllBinanceSpotSymbolsAsync();

        public Task<List<string>> GetAllBinanceUsdFutureSymbolsAsync();

        /// <summary>
        /// 下载差额数据;
        /// </summary>
        public Task DownloadBinanceSpotIncrementalDataAsync(IEnumerable<string> symbols, DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);

        public Task DownloadBinancePerpetualContractIncrementalDataAsync(IEnumerable<string> symbols, DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);
    }

    public class CryptoSourceDataService : ICryptoSourceDataService
    {
        private readonly IOService _ioService;

        public CryptoSourceDataService(IOService ioService)
        {
            _ioService = ioService;
        }

        /// <summary>
        /// 获取CoinMarketCap 市值前count的symbols
        /// </summary>
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

        #region private functions (新增的增量计算与请求函数 + 原有保存函数)

        /// <summary>
        /// floor 到步长边界（以 CloseTime 的“边界时刻”为准，如 1h 则整点、3m 则每 0/3/6... 分的 00 秒）。
        /// </summary>
        private static DateTime AlignFloorToBoundary(DateTime dt, TimeSpan step)
        {
            // 不强制转换时区，按 ticks 对齐即可；避免与外部时区混用带来误差
            long ticks = dt.Ticks - (dt.Ticks % step.Ticks);
            return new DateTime(ticks, dt.Kind);
        }

        /// <summary>
        /// ceil 到步长边界（见上）。若 dt 已在边界上，则返回原值。
        /// </summary>
        private static DateTime AlignCeilToBoundary(DateTime dt, TimeSpan step)
        {
            var floor = AlignFloorToBoundary(dt, step);
            if (floor == dt) return dt;
            return floor.Add(step);
        }

        /// <summary>
        /// 依据现有数据（用 CloseTime 为主键）与请求区间 [requestStart, requestEnd] 计算“缺口区间”（均为 CloseTime 闭区间）。
        /// 返回的 From/To 均已对齐到步长边界。
        /// </summary>
        private static IEnumerable<(DateTime From, DateTime To)> ComputeMissingRanges(
            IEnumerable<Ohlcv> existing,
            DateTime requestStart,
            DateTime requestEnd,
            TimeSpan step)
        {
            if (requestStart >= requestEnd)
                yield break;

            var startClose = AlignCeilToBoundary(requestStart, step);   // 第一个应该存在的 CloseTime
            var endClose = AlignFloorToBoundary(requestEnd, step);    // 最后一个应该存在的 CloseTime

            if (startClose > endClose)
                yield break;

            var existTimes = (existing ?? Enumerable.Empty<Ohlcv>())
                .Select(x => x.OpenDateTime)
                .Where(t => t >= startClose && t <= endClose)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // 完全没有现有数据，整个区间都是缺口
            if (existTimes.Count == 0)
            {
                yield return (startClose, endClose);
                yield break;
            }

            // 头部缺口
            if (existTimes.First() > startClose)
            {
                var to = existTimes.First().Add(-step);
                if (to >= startClose)
                    yield return (startClose, to);
            }

            // 中间缺口
            for (int i = 1; i < existTimes.Count; i++)
            {
                var prev = existTimes[i - 1];
                var curr = existTimes[i];
                var expectedNext = prev.Add(step);
                if (curr > expectedNext)
                {
                    yield return (expectedNext, curr.Add(-step));
                }
            }

            // 尾部缺口
            if (existTimes.Last() < endClose)
            {
                var from = existTimes.Last().Add(step);
                if (from <= endClose)
                    yield return (from, endClose);
            }
        }

        /// <summary>
        /// 只获取指定“收盘时间”闭区间 [closeFrom, closeTo] 的 Spot K 线，返回内存 Ohlcv 列表（不落盘）。
        /// 注意 Binance API 以 openTime 为检索条件，因此内部自动换算 openTime 范围并做分页。
        /// </summary>
        private async Task<List<Ohlcv>> FetchSpotKlinesByCloseAsync(
            BinanceRestClient client,
            string symbol,
            KlineInterval interval,
            DateTime closeFrom,
            DateTime closeTo,
            CancellationToken ct = default)
        {
            var list = new List<Ohlcv>();
            var step = UtilityService. KlineIntervalToTimeSpan(interval);
            var limit = 1000;

            // openTime 范围：包含 closeFrom/closeTo 对应的 openTime
            var openCursor = closeFrom.Add(-step);
            var openEnd = closeTo.Add(-step);

            while (openCursor <= openEnd)
            {
                var res = await client.SpotApi.ExchangeData
                    .GetKlinesAsync(symbol, interval, openCursor, openEnd, limit, ct)
                    .ConfigureAwait(false);

                if (!res.Success)
                    throw new Exception($"Spot klines failed for {symbol}: {res.Error?.Message}");

                var batch = res.Data?.ToList() ?? new List<IBinanceKline>();
                if (batch.Count == 0) break;

                foreach (var k in batch)
                {
                    // 以 CloseTime 为主键过滤到 [closeFrom, closeTo]
                    var ts = k.CloseTime;
                    if (ts < closeFrom || ts > closeTo) continue;

                    list.Add(new Ohlcv
                    {
                        OpenDateTime = ts,
                        Open = k.OpenPrice,
                        High = k.HighPrice,
                        Low = k.LowPrice,
                        Close = k.ClosePrice,
                        Volume = k.Volume
                    });
                }

                // 下一页：下一个 openTime = 当前最后一根的 CloseTime
                var lastClose = batch.Max(x => x.CloseTime);
                var nextOpen = lastClose; // 对于固定步长，下一根 openTime == 上一根 closeTime
                if (nextOpen <= openCursor) break; // 安全保护
                openCursor = nextOpen;

                await Task.Delay(30, ct).ConfigureAwait(false); // 轻微节流
            }

            return list;
        }

        /// <summary>
        /// 只获取指定“收盘时间”闭区间 [closeFrom, closeTo] 的 USD-M 永续/合约 K 线，返回内存 Ohlcv 列表（不落盘）。
        /// </summary>
        private async Task<List<Ohlcv>> FetchUsdFutureKlinesByCloseAsync(
            BinanceRestClient client,
            string symbol,
            KlineInterval interval,
            DateTime closeFrom,
            DateTime closeTo,
            CancellationToken ct = default)
        {
            var list = new List<Ohlcv>();
            var step = UtilityService.KlineIntervalToTimeSpan(interval);
            var limit = 1000;

            var openCursor = closeFrom.Add(-step);
            var openEnd = closeTo.Add(-step);

            while (openCursor <= openEnd)
            {
                var res = await client.UsdFuturesApi.ExchangeData
                    .GetKlinesAsync(symbol, interval, openCursor, openEnd, limit, ct)
                    .ConfigureAwait(false);

                if (!res.Success)
                    throw new Exception($"USDT-M klines failed for {symbol}: {res.Error?.Message}");

                var batch = res.Data?.ToList() ?? new List<IBinanceKline>();
                if (batch.Count == 0) break;

                foreach (var k in batch)
                {
                    var ts = k.CloseTime;
                    if (ts < closeFrom || ts > closeTo) continue;

                    list.Add(new Ohlcv
                    {
                        OpenDateTime = ts,
                        Open = k.OpenPrice,
                        High = k.HighPrice,
                        Low = k.LowPrice,
                        Close = k.ClosePrice,
                        Volume = k.Volume
                    });
                }

                var lastClose = batch.Max(x => x.CloseTime);
                var nextOpen = lastClose;
                if (nextOpen <= openCursor) break;
                openCursor = nextOpen;

                await Task.Delay(30, ct).ConfigureAwait(false);
            }

            return list;
        }

        /// <summary>
        /// 从Binance下载数据， 存到制定的(csv)文件（全量落盘；保留原逻辑）
        /// </summary>
        private async Task SaveSpotKlinesToCsv(string symbol, Binance.Net.Enums.KlineInterval interval, DateTime startDt, DateTime endDt, string fullPathFileName)
        {
            if (endDt > DateTime.UtcNow)
                throw new ArgumentOutOfRangeException();

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
                    if (lastDtInOhlcvs != default(DateTime))
                    {
                        paramStartDt = lastDtInOhlcvs;
                    }

                    // 获取历史K线数据
                    var klinesResult = await client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, paramStartDt, endDt);
                    if (klinesResult.Success)
                    {
                        if (klinesResult.Data.Count() == 0)
                            return;
                        ohlcvs = UpsertOhlcvs(klinesResult.Data, ohlcvs, startDt, endDt);
                    }
                    else
                    {
                        UtilityService.LogAndWriteLine($"Symbol: {symbol}, error message: {klinesResult.Error.Message}", LogEventLevel.Error);
                        return;
                    }
                    lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                }

                // Save ohlcvs to file
                lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                var firstDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).FirstOrDefault();
                if (firstDtInOhlcvs.Date != startDt)
                {
                    UtilityService.LogAndWriteLine($"firstDtInOhlcvs:{firstDtInOhlcvs} does not match with startDt:{startDt}");
                    return;
                }
                if (lastDtInOhlcvs.Date != endDt)
                {
                    UtilityService.LogAndWriteLine($"lastDtInOhlcvs:{firstDtInOhlcvs} does not match with endDt:{endDt}");
                    return;
                }

                _ioService.WriteCsvManually(fullPathFileName, ohlcvs);

                UtilityService.LogAndWriteLine($"Klines data saved successfully for {symbol}.");
            }
        }

        /// <summary>
        /// USD-M 合约全量落盘（保留原逻辑）
        /// </summary>
        private async Task SaveUsdFutureKlinesToCsv(string symbol, Binance.Net.Enums.KlineInterval interval, DateTime startDt, DateTime endDt, string fullPathFileName)
        {
            if (endDt > DateTime.UtcNow)
                throw new ArgumentOutOfRangeException();

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
                    if (lastDtInOhlcvs != default(DateTime))
                    {
                        paramStartDt = lastDtInOhlcvs;
                    }

                    var klinesResult = await client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, interval, paramStartDt, endDt);
                    if (klinesResult.Success)
                    {
                        if (klinesResult.Data.Count() == 0)
                            return;
                        ohlcvs = UpsertOhlcvs(klinesResult.Data, ohlcvs, startDt, endDt);
                    }
                    else
                    {
                        throw new Exception($"Error fetching klines for {symbol}: {klinesResult.Error.Message}");
                    }
                    lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                }

                lastDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).LastOrDefault();
                var firstDtInOhlcvs = ohlcvs.Select(x => x.OpenDateTime).FirstOrDefault();
                if (firstDtInOhlcvs.Date != startDt)
                {
                    UtilityService.LogAndWriteLine($"firstDtInOhlcvs:{firstDtInOhlcvs} does not match with startDt:{startDt}");
                    return;
                }
                if (lastDtInOhlcvs.Date != endDt)
                {
                    UtilityService.LogAndWriteLine($"lastDtInOhlcvs:{firstDtInOhlcvs} does not match with endDt:{endDt}");
                    return;
                }

                _ioService.WriteCsvManually(fullPathFileName, ohlcvs);

                UtilityService.LogAndWriteLine($"Klines data saved successfully for {symbol}.");
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
                    OpenDateTime = kline.OpenTime,
                    CloseDateTime = kline.CloseTime,
                    Open = kline.OpenPrice,
                    High = kline.HighPrice,
                    Low = kline.LowPrice,
                    Close = kline.ClosePrice,
                    AdjustedClose = kline.ClosePrice,
                    Volume = kline.Volume
                };

                ohlcvs.Add(ohlcv);
                if (kline.CloseTime > endDt)
                    break;
            }
            return ohlcvs;
        }

        #endregion

        public async Task DownloadBinanceSpotAsync(
            IEnumerable<string> symbols,
            DateTime startDt,
            DateTime endDt,
            string path = "",
            KlineInterval klineInterval = KlineInterval.OneHour)
        {
            var symbolList = symbols?.ToList();
            if (symbolList is null || symbolList.Count == 0)
            {
                UtilityService.LogAndWriteLine("No symbols provided for spot download. Aborting.");
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "spot");
            }
            Directory.CreateDirectory(path);

            var interval = klineInterval;

            UtilityService.LogAndWriteLine($"Starting Spot data download for {symbolList.Count} symbols from {startDt:yyyy-MM-dd} to {endDt:yyyy-MM-dd} at {interval} interval.");

            var binanceSymbols = await GetAllBinanceSpotSymbolsAsync();
            foreach (var symbol in symbolList)
            {
                try
                {
                    UtilityService.LogAndWriteLine($"Downloading spot: {symbol}...");
                    var fileName = $"{symbol}.csv";
                    var fullPathFileName = Path.Combine(path, fileName);

                    await SaveSpotKlinesToCsv(symbol, interval, startDt, endDt, fullPathFileName);

                    UtilityService.LogAndWriteLine($"Successfully saved spot: {symbol}");
                }
                catch (Exception ex)
                {
                    UtilityService.LogAndWriteLine($"Current utc datetime: {DateTime.UtcNow}. Error downloading spot data for {symbol}: {ex.Message}", LogEventLevel.Error);
                }
            }

            UtilityService.LogAndWriteLine("Spot data download for all specified symbols done!");
        }

        public async Task DownloadBinanceUsdFutureAsync(
            IEnumerable<string> symbols,
            DateTime startDt,
            DateTime endDt,
            string path = "",
            KlineInterval klineInterval = KlineInterval.OneHour)
        {
            var symbolList = symbols?.ToList();
            if (symbolList is null || symbolList.Count == 0)
            {
                UtilityService.LogAndWriteLine("No symbols provided for USD Future download. Aborting.");
                return;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "UsdFuture");
            }
            Directory.CreateDirectory(path);

            var interval = klineInterval;

            UtilityService.LogAndWriteLine($"Starting USD Future data download for {symbolList.Count} symbols from {startDt:yyyy-MM-dd} to {endDt:yyyy-MM-dd} at {interval} interval.");

            foreach (var symbol in symbolList)
            {
                try
                {
                    if (symbol.Equals("BTCSTUSDT", StringComparison.OrdinalIgnoreCase))
                        continue;

                    UtilityService.LogAndWriteLine($"Downloading USD Future: {symbol}...");
                    var fileName = $"{symbol}.csv";
                    var fullPathFileName = Path.Combine(path, fileName);

                    await SaveUsdFutureKlinesToCsv(symbol, interval, startDt, endDt, fullPathFileName);

                    UtilityService.LogAndWriteLine($"Successfully saved USD Future: {symbol}");
                }
                catch (Exception ex)
                {
                    UtilityService.LogAndWriteLine($"Current utc datetime: {DateTime.UtcNow}. Error downloading USD Future data for {symbol}: {ex.Message}", LogEventLevel.Error);
                    Thread.Sleep(200);
                    continue;
                }
            }

            UtilityService.LogAndWriteLine("USD Future data download are done!");
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

        public async Task DownloadBinanceSpotIncrementalDataAsync(
            IEnumerable<string> symbols,
            DateTime startDt,
            DateTime endDt,
            string path = "",
            KlineInterval klineInterval = KlineInterval.OneHour)
        {
            // 1) 校验
            if (symbols == null || !symbols.Any())
            {
                UtilityService.LogAndWriteLine("Spot incremental skipped: Symbol list is empty or null.", LogEventLevel.Warning);
                throw new ArgumentException("Symbol list cannot be null or empty.", nameof(symbols));
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                UtilityService.LogAndWriteLine("Spot incremental failed: Destination path is null or whitespace.", LogEventLevel.Error);
                throw new ArgumentNullException(nameof(path), "Destination path cannot be null or empty.");
            }
            if (startDt >= endDt)
            {
                string msg = $"Spot incremental failed due to invalid date range. StartDt: {startDt:yyyy-MM-dd HH:mm}, EndDt: {endDt:yyyy-MM-dd HH:mm}.";
                UtilityService.LogAndWriteLine(msg, LogEventLevel.Error);
                throw new ArgumentException("Start date must be strictly before end date.", nameof(startDt));
            }

            // 2) 路径
            await UtilityService.IsPathExistAsync(path);
            var step = UtilityService.KlineIntervalToTimeSpan(klineInterval);

            // 3) 统一创建一个 client 复用
            using var client = new BinanceRestClient();

            foreach (var tmpSymbol in symbols)
            {
                try
                {
                    var fullPathFileName = Path.Combine(path, $"{tmpSymbol}.csv");

                    // 文件不存在：直接做一次全量
                    if (!File.Exists(fullPathFileName))
                    {
                        await DownloadBinanceSpotAsync(new[] { tmpSymbol }, startDt, endDt, path, klineInterval);
                        UtilityService.LogAndWriteLine($"Initial full data download completed for Spot {tmpSymbol}.", LogEventLevel.Information);
                        continue;
                    }

                    // 文件存在：读取现有数据，计算缺口
                    var existOhlcvs = _ioService.ReadCsv(fullPathFileName);
                    var existSet = existOhlcvs?.OhlcvSet ?? new HashSet<Ohlcv>();
                    var missingRanges = ComputeMissingRanges(existSet, startDt, endDt, step).ToList();

                    if (missingRanges.Count == 0)
                    {
                        // 没缺口，仅裁剪到请求区间（包含 endDt 的那根）
                        var trimmed = existSet
                            .Where(d => d.OpenDateTime >= startDt && d.OpenDateTime <= endDt)
                            .OrderBy(d => d.OpenDateTime)
                            .ToList();

                        _ioService.WriteCsvManually(fullPathFileName, trimmed);
                        UtilityService.LogAndWriteLine($"[Spot {tmpSymbol}] already complete; trimmed to [{startDt:yyyy-MM-dd HH:mm}, {endDt:yyyy-MM-dd HH:mm}].", LogEventLevel.Information);
                        continue;
                    }

                    // 逐缺口抓取并聚合
                    var fetchedAll = new List<Ohlcv>();
                    foreach (var (from, to) in missingRanges)
                    {
                        var slice = await FetchSpotKlinesByCloseAsync(client, tmpSymbol, klineInterval, from, to);
                        fetchedAll.AddRange(slice);
                    }

                    // 合并去重（以 CloseTime 为键），并裁剪到请求区间
                    var merged = existSet
                        .Concat(fetchedAll)
                        .GroupBy(x => x.OpenDateTime)
                        .Select(g => g.First())
                        .Where(x => x.OpenDateTime >= startDt && x.OpenDateTime <= endDt)
                        .OrderBy(x => x.OpenDateTime)
                        .ToList();

                    _ioService.WriteCsvManually(fullPathFileName, merged);

                    UtilityService.LogAndWriteLine(
                        $"[Spot {tmpSymbol}] incremental merged: +{fetchedAll.Count} bars; total {merged.Count} in [{startDt:yyyy-MM-dd HH:mm}, {endDt:yyyy-MM-dd HH:mm}].",
                        LogEventLevel.Information);
                }
                catch (Exception ex)
                {
                    UtilityService.LogAndWriteLine($"Spot incremental error for {tmpSymbol}: {ex.Message}", LogEventLevel.Error);
                    // 不中断，继续处理其他 symbol
                }
            }
        }

        public async Task DownloadBinancePerpetualContractIncrementalDataAsync(
            IEnumerable<string> symbols,
            DateTime startDt,
            DateTime endDt,
            string path = "",
            KlineInterval klineInterval = KlineInterval.OneHour)
        {
            // 1) 校验
            if (symbols == null || !symbols.Any())
            {
                UtilityService.LogAndWriteLine("Perpetual contract sync skipped: Symbol list is empty or null.", LogEventLevel.Warning);
                throw new ArgumentException("Symbol list cannot be null or empty.", nameof(symbols));
            }
            if (string.IsNullOrWhiteSpace(path))
            {
                UtilityService.LogAndWriteLine("Perpetual contract sync failed: Destination path is null or whitespace.", LogEventLevel.Error);
                throw new ArgumentNullException(nameof(path), "Destination path cannot be null or empty.");
            }
            if (startDt >= endDt)
            {
                string msg = $"Perpetual contract sync failed due to invalid date range. StartDt: {startDt:yyyy-MM-dd HH:mm}, EndDt: {endDt:yyyy-MM-dd HH:mm}.";
                UtilityService.LogAndWriteLine(msg, LogEventLevel.Error);
                throw new ArgumentException("Start date must be strictly before end date.", nameof(startDt));
            }

            // 2) 路径
            await UtilityService.IsPathExistAsync(path);
            var step = UtilityService.KlineIntervalToTimeSpan(klineInterval);

            // 3) 复用一个 client
            using var client = new BinanceRestClient();

            foreach (var tmpSymbol in symbols)
            {
                try
                {
                    var fullPathFileName = Path.Combine(path, $"{tmpSymbol}.csv");

                    if (!File.Exists(fullPathFileName))
                    {
                        await DownloadBinanceUsdFutureAsync(new[] { tmpSymbol }, startDt, endDt, path, klineInterval);
                        UtilityService.LogAndWriteLine($"Initial full data download completed for Perpetual {tmpSymbol}.", LogEventLevel.Information);
                        continue;
                    }

                    var existOhlcvs = _ioService.ReadCsv(fullPathFileName);
                    var existSet = existOhlcvs?.OhlcvSet ?? new HashSet<Ohlcv>();

                    var missingRanges = ComputeMissingRanges(existSet, startDt, endDt, step).ToList();

                    if (missingRanges.Count == 0)
                    {
                        var trimmed = existSet
                            .Where(d => d.OpenDateTime >= startDt && d.OpenDateTime <= endDt)
                            .OrderBy(d => d.OpenDateTime)
                            .ToList();

                        _ioService.WriteCsvManually(fullPathFileName, trimmed);
                        UtilityService.LogAndWriteLine($"[Perp {tmpSymbol}] already complete; trimmed to [{startDt:yyyy-MM-dd HH:mm}, {endDt:yyyy-MM-dd HH:mm}].", LogEventLevel.Information);
                        continue;
                    }

                    var fetchedAll = new List<Ohlcv>();
                    foreach (var (from, to) in missingRanges)
                    {
                        var slice = await FetchUsdFutureKlinesByCloseAsync(client, tmpSymbol, klineInterval, from, to);
                        fetchedAll.AddRange(slice);
                    }

                    var merged = existSet
                        .Concat(fetchedAll)
                        .GroupBy(x => x.OpenDateTime)
                        .Select(g => g.First())
                        .Where(x => x.OpenDateTime >= startDt && x.OpenDateTime <= endDt)
                        .OrderBy(x => x.OpenDateTime)
                        .ToList();

                    _ioService.WriteCsvManually(fullPathFileName, merged);

                    UtilityService.LogAndWriteLine(
                        $"[Perp {tmpSymbol}] incremental merged: +{fetchedAll.Count} bars; total {merged.Count} in [{startDt:yyyy-MM-dd HH:mm}, {endDt:yyyy-MM-dd HH:mm}].",
                        LogEventLevel.Information);
                }
                catch (Exception ex)
                {
                    UtilityService.LogAndWriteLine($"Perpetual incremental error for {tmpSymbol}: {ex.Message}", LogEventLevel.Error);
                }
            }
        }

        

    }
}
