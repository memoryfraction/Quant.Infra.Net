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
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Service
{
    public class TraditionalFinanceSourceDataService : ITraditionalFinanceSourceDataService
    {
        private readonly IMapper _mapper;
        private readonly IHistoricalDataSourceService _historicalDataSourceService;

        /// <summary>
        /// 构造函数。
        /// Constructor for TraditionalFinanceSourceDataService.
        /// </summary>
        /// <param name="mapper">AutoMapper instance.</param>
        /// <param name="historicalDataSourceService">Historical data source service.</param>
        public TraditionalFinanceSourceDataService(IMapper mapper, IHistoricalDataSourceService historicalDataSourceService)
        {
            if (mapper == null) throw new ArgumentNullException(nameof(mapper));
            if (historicalDataSourceService == null) throw new ArgumentNullException(nameof(historicalDataSourceService));

            _mapper = mapper;
            _historicalDataSourceService = historicalDataSourceService;
        }

        /// <summary>
        /// Begin syncing source daily data (not implemented).
        /// 开始同步每日数据（未实现）。
        /// </summary>
        public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, Shared.Model.ResolutionLevel Period = Shared.Model.ResolutionLevel.Daily)
        {
            if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("symbol must not be null or empty.", nameof(symbol));
            if (startDt > endDt) throw new ArgumentException("startDt must be earlier than or equal to endDt.", nameof(startDt));
            if (string.IsNullOrWhiteSpace(fullPathFileName)) throw new ArgumentException("fullPathFileName must not be null or empty.", nameof(fullPathFileName));
            return Task.FromException<Ohlcvs>(new NotImplementedException());
        }

        /// <summary>
        /// Download Ohlcv list for a traditional finance symbol from the configured historical source.
        /// 从配置的历史数据源下载指定标的的 Ohlcv 列表。
        /// </summary>
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

            return ohlcvs;
        }


      

        /// <summary>
        /// Read Ohlcv list from a CSV file.
        /// 从 CSV 文件读取 Ohlcv 列表。
        /// </summary>
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
        /// 获取 S&P500 的成分股 symbol 列表。
        /// </summary>
        /// <summary>
        /// 获取 S&P500 的成分股 symbol 列表。
        /// Get S&P500 constituent symbols.
        /// </summary>
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
        /// Save Ohlcv list to CSV.
        /// 将 Ohlcv 列表保存到 CSV 文件。
        /// </summary>
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
    }
}