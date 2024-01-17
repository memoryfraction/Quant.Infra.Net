using AutoMapper;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Threading.Tasks;
using YahooFinanceApi;

namespace Quant.Infra.Net.SourceData.Service
{
    public class SourceDataService : ISourceDataService
    {
        private bool _isBusy;
        private readonly IMapper _mapper;
        public SourceDataService(IMapper mapper)
        {
            _mapper = mapper;
        }


        public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, Shared.Model.Period Period = Shared.Model.Period.Daily)
        {
            throw new NotImplementedException();
        }

        public async Task<Ohlcvs> GetOhlcvsAsync(string symbol, DateTime startDt, DateTime endDt, Shared.Model.Period period = Shared.Model.Period.Daily, DataSource dataSource = DataSource.YahooFinance)
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
    }
}