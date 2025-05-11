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

        public TraditionalFinanceSourceDataService(IMapper mapper, IHistoricalDataSourceService historicalDataSourceService)
        {
            _mapper = mapper;
            _historicalDataSourceService = historicalDataSourceService;
        }

        public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, Shared.Model.ResolutionLevel Period = Shared.Model.ResolutionLevel.Daily)
        {
            throw new NotImplementedException();
        }

        public async Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, Shared.Model.ResolutionLevel period = Shared.Model.ResolutionLevel.Daily, DataSource dataSource = DataSource.MongoDBWebApi)
        {
            var ohlcvs = new Ohlcvs();
            
            if(dataSource == DataSource.MongoDBWebApi)
            { 
                // 根据_historicalDataSourceService 获取历史数据，并返回类型，忽略dataSource;
                ohlcvs.OhlcvSet = (await  _historicalDataSourceService.GetOhlcvListAsync(new Underlying(symbol, AssetType.UsEquity), startDt, endDt, period)).ToHashSet();
            
                ohlcvs.Symbol = symbol;
                ohlcvs.StartDateTimeUtc = startDt;
                ohlcvs.EndDateTimeUtc = endDt;
                ohlcvs.ResolutionLevel = period;
            }

            return ohlcvs;
        }


      

        public async Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFileName)
        {
            if (!File.Exists(fullPathFileName))
                throw new ArgumentNullException($"File not found: {fullPathFileName}");
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

        public async Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500)
        {
            var url = "https://en.wikipedia.org/wiki/List_of_S%26P_500_companies";
            var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(url);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var tickers = new List<string>();
            var nodes = htmlDoc.DocumentNode.SelectNodes("//table[contains(@class, 'wikitable')][1]//tr/td[1]/a");

            foreach (var node in nodes)
            {
                tickers.Add(node.InnerText);
            }

            return tickers.Take(number).Order();
        }

        public async Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName)
        {
            if (!File.Exists(fullPathFileName))
                await UtilityService.IsPathExistAsync(fullPathFileName);

            if (ohlcvList == null || !ohlcvList.Any())
                throw new ArgumentNullException("ohlcvList is null");

            await UtilityService.IsPathExistAsync(fullPathFileName);

            // save
            using var writer = new StreamWriter(fullPathFileName);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csv.WriteRecordsAsync(ohlcvList);
        }
    }
}