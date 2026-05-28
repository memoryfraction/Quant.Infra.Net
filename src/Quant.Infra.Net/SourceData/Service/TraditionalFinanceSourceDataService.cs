using AutoMapper;
using CsvHelper;
using HtmlAgilityPack;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service.Historical;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Service
{
    /// <summary>
    /// 传统金融数据源服务，提供股票等传统金融资产的数据获取功能。
    /// Traditional finance data source service, provides data retrieval functionality for traditional financial assets like stocks.
    /// </summary>
    public class TraditionalFinanceSourceDataService : ITraditionalFinanceSourceDataService
    {
        private readonly IMapper _mapper;
        private readonly IHistoricalDataSourceService _historicalDataSourceService;

        /// <summary>
        /// 初始化传统金融数据源服务。
        /// Initializes the traditional finance data source service.
        /// </summary>
        /// <param name="mapper">AutoMapper实例 / AutoMapper instance.</param>
        /// <param name="historicalDataSourceService">历史数据源服务 / Historical data source service.</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出 / Thrown when parameters are null.</exception>
        public TraditionalFinanceSourceDataService(IMapper mapper, IHistoricalDataSourceService historicalDataSourceService)
        {
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            if (historicalDataSourceService == null) throw new ArgumentNullException(nameof(historicalDataSourceService));

            _mapper = mapper;
            _historicalDataSourceService = historicalDataSourceService;
        }

        /// <summary>
        /// 开始同步每日数据（未实现）。
        /// Begin syncing source daily data (not implemented).
        /// </summary>
        /// <param name="symbol">交易符号 / Trading symbol.</param>
        /// <param name="startDt">开始时间 / Start date.</param>
        /// <param name="endDt">结束时间 / End date.</param>
        /// <param name="fullPathFileName">完整文件路径 / Full file path.</param>
        /// <param name="Period">分辨率级别 / Resolution level.</param>
        /// <returns>OHLCV数据集合 / OHLCV data collection.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出 / Thrown when parameters are invalid.</exception>
        public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, Shared.Model.ResolutionLevel Period = Shared.Model.ResolutionLevel.Daily)
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("symbol must not be null or empty.", nameof(symbol));
            if (startDt > endDt) throw new ArgumentException("startDt must be earlier than or equal to endDt.", nameof(startDt));
            if (string.IsNullOrWhiteSpace(fullPathFileName)) throw new ArgumentException("fullPathFileName must not be null or empty.", nameof(fullPathFileName));
            return Task.FromException<Ohlcvs>(new NotImplementedException());
        }

        /// <summary>
        /// 从配置的历史数据源下载指定标的的OHLCV列表。
        /// Download OHLCV list for a traditional finance symbol from the configured historical source.
        /// </summary>
        /// <param name="symbol">交易符号 / Trading symbol.</param>
        /// <param name="startDt">开始时间 / Start date.</param>
        /// <param name="endDt">结束时间 / End date.</param>
        /// <param name="period">分辨率级别 / Resolution level.</param>
        /// <param name="dataSource">数据源类型 / Data source type.</param>
        /// <returns>OHLCV数据集合 / OHLCV data collection.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出 / Thrown when parameters are invalid.</exception>
        public async Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, Shared.Model.ResolutionLevel period = Shared.Model.ResolutionLevel.Daily, DataSource dataSource = DataSource.MongoDBWebApi)
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("symbol must not be null or empty.", nameof(symbol));
            if (startDt > endDt) throw new ArgumentException("startDt must be earlier than or equal to endDt.", nameof(startDt));
            if (startDt == endDt) throw new ArgumentException("startDt and endDt must not be equal.", nameof(startDt));

            var ohlcvs = new Ohlcvs();
            if (dataSource == DataSource.MongoDBWebApi)
            {
                // 根据_historicalDataSourceService 获取历史数据，并返回类型，忽略dataSource;
                var result = await _historicalDataSourceService.GetOhlcvListAsync(new Underlying(symbol, AssetType.UsEquity), startDt, endDt, period);
                ohlcvs.OhlcvSet = result != null ? result.ToHashSet() : new HashSet<Ohlcv>();

                ohlcvs.Symbol = symbol;
                ohlcvs.StartDateTimeUtc = startDt;
                ohlcvs.EndDateTimeUtc = endDt;
                ohlcvs.ResolutionLevel = period;
            }
            else if (dataSource == DataSource.YahooFinance)
            {
                var ohlcvList = await DownloadOhlcvListFromYahooFinanceAsync(symbol, startDt, endDt, period);
                ohlcvs.OhlcvSet = ohlcvList.ToHashSet();
                ohlcvs.Symbol = symbol;
                ohlcvs.StartDateTimeUtc = startDt;
                ohlcvs.EndDateTimeUtc = endDt;
                ohlcvs.ResolutionLevel = period;
            }

            return ohlcvs;
        }


      

        /// <summary>
        /// 从CSV文件读取OHLCV列表。
        /// Read OHLCV list from a CSV file.
        /// </summary>
        /// <param name="fullPathFileName">CSV文件完整路径 / Full path of the CSV file.</param>
        /// <returns>OHLCV数据列表 / List of OHLCV data.</returns>
        /// <exception cref="ArgumentException">当文件路径无效时抛出 / Thrown when file path is invalid.</exception>
        /// <exception cref="FileNotFoundException">当文件不存在时抛出 / Thrown when file does not exist.</exception>
        public async Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFileName)
        {
            if (string.IsNullOrWhiteSpace(fullPathFileName)) throw new ArgumentException("fullPathFileName must not be null or empty.", nameof(fullPathFileName));
            if (!File.Exists(fullPathFileName))
                throw new FileNotFoundException($"File not found: {fullPathFileName}");

            var ohlcvList = new List<Ohlcv>();

            using (var reader = new StreamReader(fullPathFileName))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    await csv.ReadAsync();
                    csv.ReadHeader();
                    while (await csv.ReadAsync())
                    {
                        var ohlcv = csv.GetRecord<Ohlcv>();
                        ohlcvList.Add(ohlcv);
                    }
                }
            }
            return ohlcvList;
        }

        /// <summary>
        /// 获取S&P 500成分股符号列表。
        /// Gets S&P 500 constituent symbols.
        /// </summary>
        /// <param name="number">获取数量（默认500）/ Number of symbols to retrieve (default 500).</param>
        /// <returns>S&P 500成分股符号列表 / List of S&P 500 constituent symbols.</returns>
        /// <exception cref="ArgumentOutOfRangeException">当number为非正数时抛出 / Thrown when number is not positive.</exception>
        /// <exception cref="Exception">当解析Wikipedia表格失败时抛出 / Thrown when parsing Wikipedia table fails.</exception>
        public async Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500)
        {
            var url = "https://en.wikipedia.org/wiki/List_of_S%26P_500_companies";

            using var httpClient = new HttpClient();

            // 必须设置 User-Agent，否则会 403
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            var html = await httpClient.GetStringAsync(url);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var tickers = new List<string>();
            var nodes = htmlDoc.DocumentNode.SelectNodes("//table[contains(@class, 'wikitable')][1]//tr/td[1]/a");

            if (number <= 0) throw new ArgumentOutOfRangeException(nameof(number), "number must be positive.");
            if (nodes == null || nodes.Count == 0)
                throw new Exception("Failed to parse Wikipedia table. XPath may have changed.");

            foreach (var node in nodes)
            {
                tickers.Add(node.InnerText.Trim());
            }

            return tickers.Take(number).OrderBy(x => x);
        }

        /// <summary>
        /// 将OHLCV列表保存到CSV文件。
        /// Save OHLCV list to CSV file.
        /// </summary>
        /// <param name="ohlcvList">OHLCV数据列表 / List of OHLCV data.</param>
        /// <param name="fullPathFileName">CSV文件完整路径 / Full path of the CSV file.</param>
        /// <exception cref="ArgumentException">当文件路径无效时抛出 / Thrown when file path is invalid.</exception>
        /// <exception cref="ArgumentNullException">当ohlcvList为null时抛出 / Thrown when ohlcvList is null.</exception>
        public async Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName)
        {
            if (string.IsNullOrWhiteSpace(fullPathFileName)) throw new ArgumentException("fullPathFileName must not be null or empty.", nameof(fullPathFileName));
            if (ohlcvList == null) throw new ArgumentNullException(nameof(ohlcvList));
            if (!ohlcvList.Any()) return; // silently skip empty list

            if (!File.Exists(fullPathFileName))
                await UtilityService.IsPathExistAsync(fullPathFileName);

            await UtilityService.IsPathExistAsync(fullPathFileName);

            // save
            using var writer = new StreamWriter(fullPathFileName);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(ohlcvList);
        }

        /// <summary>
        /// 从Yahoo Finance Chart API下载OHLCV数据。
        /// Download OHLCV data from Yahoo Finance Chart API.
        /// </summary>
        private async Task<List<Ohlcv>> DownloadOhlcvListFromYahooFinanceAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel period)
        {
            var interval = ConvertResolutionLevelToYahooInterval(period);
            var period1 = new DateTimeOffset(startDt.ToUniversalTime()).ToUnixTimeSeconds();
            var period2 = new DateTimeOffset(endDt.ToUniversalTime()).ToUnixTimeSeconds();

            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?period1={period1}&period2={period2}&interval={interval}&includePrePost=false";

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            var json = await httpClient.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            var chart = root.GetProperty("chart");

            // 检查API是否返回错误
            if (chart.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null && error.ValueKind != JsonValueKind.Undefined)
            {
                return new List<Ohlcv>();
            }

            var result = chart.GetProperty("result")[0];
            var timestamps = result.GetProperty("timestamp").EnumerateArray().Select(t => t.GetInt64()).ToArray();
            var quote = result.GetProperty("indicators").GetProperty("quote")[0];

            var opens = quote.GetProperty("open").EnumerateArray().Select(o => o.ValueKind == JsonValueKind.Null ? (decimal?)null : o.GetDecimal()).ToArray();
            var highs = quote.GetProperty("high").EnumerateArray().Select(o => o.ValueKind == JsonValueKind.Null ? (decimal?)null : o.GetDecimal()).ToArray();
            var lows = quote.GetProperty("low").EnumerateArray().Select(o => o.ValueKind == JsonValueKind.Null ? (decimal?)null : o.GetDecimal()).ToArray();
            var closes = quote.GetProperty("close").EnumerateArray().Select(o => o.ValueKind == JsonValueKind.Null ? (decimal?)null : o.GetDecimal()).ToArray();
            var volumes = quote.GetProperty("volume").EnumerateArray().Select(o => o.ValueKind == JsonValueKind.Null ? (decimal?)null : o.GetDecimal()).ToArray();

            var ohlcvList = new List<Ohlcv>();
            for (int i = 0; i < timestamps.Length; i++)
            {
                if (opens[i] == null || highs[i] == null || lows[i] == null || closes[i] == null || volumes[i] == null)
                    continue;

                var openDateTime = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).UtcDateTime;
                DateTime closeDateTime;

                switch (period)
                {
                    case ResolutionLevel.Hourly:
                        closeDateTime = openDateTime.AddHours(1).AddSeconds(-1);
                        break;
                    case ResolutionLevel.Daily:
                        closeDateTime = openDateTime.Date.AddDays(1).AddSeconds(-1);
                        break;
                    case ResolutionLevel.Weekly:
                        closeDateTime = openDateTime.AddDays(7).AddSeconds(-1);
                        break;
                    case ResolutionLevel.Monthly:
                        closeDateTime = openDateTime.AddMonths(1).AddSeconds(-1);
                        break;
                    default:
                        closeDateTime = openDateTime;
                        break;
                }

                ohlcvList.Add(new Ohlcv
                {
                    Symbol = symbol,
                    OpenDateTime = openDateTime,
                    CloseDateTime = closeDateTime,
                    Open = opens[i].Value,
                    High = highs[i].Value,
                    Low = lows[i].Value,
                    Close = closes[i].Value,
                    Volume = volumes[i].Value,
                    AdjustedClose = closes[i].Value
                });
            }

            return ohlcvList;
        }

        /// <summary>
        /// 将ResolutionLevel转换为Yahoo Finance API的interval参数。
        /// Converts ResolutionLevel to Yahoo Finance API interval parameter.
        /// </summary>
        private static string ConvertResolutionLevelToYahooInterval(ResolutionLevel period)
        {
            return period switch
            {
                ResolutionLevel.Minute => "1m",
                ResolutionLevel.Hourly => "1h",
                ResolutionLevel.Daily => "1d",
                ResolutionLevel.Weekly => "1wk",
                ResolutionLevel.Monthly => "1mo",
                _ => "1d"
            };
        }
    }
}