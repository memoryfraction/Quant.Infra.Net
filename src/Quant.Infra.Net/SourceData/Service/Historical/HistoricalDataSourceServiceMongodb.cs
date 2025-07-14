using Microsoft.Data.Analysis;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Polly;
using Polly.Retry;
using RestSharp;
using System.Globalization;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using Quant.Infra.Net.Shared.Service;
using System.Linq;

namespace Quant.Infra.Net.SourceData.Service.Historical
{
    public class HistoricalDataSourceServiceMongodb : IHistoricalDataSourceService
    {
        private readonly string _endpoint;
        private readonly RestClient _client;
        private readonly AsyncRetryPolicy<RestResponse> _retryPolicy;
        private readonly ResolutionConversionService _resolutionConversionService;

        public Currency BaseCurrency { get; set; } = Currency.USD;

        public HistoricalDataSourceServiceMongodb(string endpoint, ResolutionConversionService resolutionConversionService)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _client = new RestClient();
            _resolutionConversionService = resolutionConversionService ?? throw new ArgumentNullException(nameof(resolutionConversionService));

            _retryPolicy = Policy
                .HandleResult<RestResponse>(r => !r.IsSuccessful || string.IsNullOrWhiteSpace(r.Content))
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (response, timespan, retryCount, context) =>
                    {
                        Console.WriteLine($"[WARN] Retry #{retryCount} after {timespan.TotalSeconds} seconds. Status: {response?.Result?.StatusCode}, Error: {response?.Result?.ErrorMessage}");
                    });
        }

        public Task<DataFrame> GetHistoricalDataFrameAsync(Underlying underlying, DateTime startDate, DateTime endDate, ResolutionLevel resolutionLevel)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(Underlying underlying, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            if (resolutionLevel < ResolutionLevel.Hourly)
                throw new NotSupportedException($"Cannot convert from hourly to {resolutionLevel} granularity.");

            // 调用FetchRawOhlcvHourlyAsync获取小时级别的数据;
            var rawOhlcvHourlyList = await FetchRawOhlcvHourlyAsync(underlying, startDt, endDt);
            
            // 上一个步骤的结果转化为需要的级别，比如：Daily, Weekly, Monthly等;
            var ohlcvs = _resolutionConversionService.ConvertResolution(rawOhlcvHourlyList, resolutionLevel);

            return ohlcvs.OhlcvSet.ToList();
        }


        /// <summary>
        /// 从数据库读取小时级别数据的原始拉取方法
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        private async Task<List<Ohlcv>> FetchRawOhlcvHourlyAsync(
            Underlying underlying,
            DateTime startDt,
            DateTime endDt)
        {
            var results = new List<Ohlcv>();
            int page = 1;
            int pageSize = 5000;
            int resolutionCode = (int)ResolutionLevel.Hourly;

            while (true)
            {
                var request = new RestRequest(_endpoint, Method.Get);
                request.AddParameter("symbol", underlying.Symbol);
                request.AddParameter("resolution", resolutionCode);
                request.AddParameter("start", startDt.ToString("yyyy-M-d", CultureInfo.InvariantCulture));
                request.AddParameter("assetType", 1);
                request.AddParameter("page", page);
                request.AddParameter("pageSize", pageSize);

                Console.WriteLine($"[INFO] Fetching page {page}: {underlying.Symbol}, {startDt:yyyy-MM-dd} ~ {endDt:yyyy-MM-dd}, {ResolutionLevel.Hourly}");

                var response = await _retryPolicy.ExecuteAsync(() => _client.ExecuteAsync(request));

                if (!response.IsSuccessful)
                    throw new Exception($"Request failed: {response.StatusCode} - {response.ErrorMessage} - Content: {response.Content}");

                var json = System.Text.Json.JsonDocument.Parse(response.Content);
                var data = json.RootElement.GetProperty("data");

                if (data.GetArrayLength() == 0)
                    break;

                foreach (var item in data.EnumerateArray())
                {
                    var dt = item.GetProperty("datetime").GetDateTime();
                    if (dt > endDt) break;

                    results.Add(new Ohlcv
                    {
                        Symbol = underlying.Symbol,
                        OpenDateTime = dt,
                        Open = item.GetProperty("open").GetDecimal(),
                        High = item.GetProperty("high").GetDecimal(),
                        Low = item.GetProperty("low").GetDecimal(),
                        Close = item.GetProperty("close").GetDecimal(),
                        Volume = item.GetProperty("volume").GetDecimal()
                    });
                }

                if (data.GetArrayLength() < pageSize)
                    break;

                page++;
            }

            return results;
        }
    }
}
