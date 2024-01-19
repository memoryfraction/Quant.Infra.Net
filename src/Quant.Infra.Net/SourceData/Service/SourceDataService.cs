using AutoMapper;
using CsvHelper;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Quant.Infra.Net.Shared.Service;
using YahooFinanceApi;

namespace Quant.Infra.Net.SourceData.Service
{
    public class SourceDataService : ISourceDataService
    {
        private readonly IMapper _mapper;

        public SourceDataService(IMapper mapper)
        {
            _mapper = mapper;
        }

        public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, Shared.Model.Period Period = Shared.Model.Period.Daily)
        {
            throw new NotImplementedException();
        }

        public async Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, Shared.Model.Period period = Shared.Model.Period.Daily, DataSource dataSource = DataSource.YahooFinance)
        {
            var ohlcvs = new Ohlcvs();
            var yahooFinancePeriod = _mapper.Map<YahooFinanceApi.Period>(period);
            var candles = await Yahoo.GetHistoricalAsync(symbol, startDt, endDt, yahooFinancePeriod); // Daily, Weekly, Monthly
            foreach (var candle in candles)
            {
                var ohlcv = _mapper.Map<Ohlcv>(candle);
                ohlcvs.OhlcvList.Add(ohlcv);
            }
            ohlcvs.Symbol = symbol;
            ohlcvs.StartDateTimeUtc = startDt;
            ohlcvs.EndDateTimeUtc = endDt;
            ohlcvs.Period = period;
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