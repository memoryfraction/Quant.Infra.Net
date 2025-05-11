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

namespace Quant.Infra.Net.SourceData.Service.Historical
{
    public class HistoricalDataSourceServiceMongodb : IHistoricalDataSourceService
    {
        private readonly string _endpoint;
        private readonly RestClient _client;
        private readonly AsyncRetryPolicy<RestResponse> _retryPolicy;

        public Currency BaseCurrency { get; set; } = Currency.USD;

        public HistoricalDataSourceServiceMongodb(string endpoint)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _client = new RestClient();

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
            var results = new List<Ohlcv>();
            int page = 1;
            int pageSize = 500;
            int resolutionCode = (int)resolutionLevel;

            while (true)
            {
                var request = new RestRequest(_endpoint, Method.Get);
                request.AddParameter("symbol", underlying.Symbol);
                request.AddParameter("resolution", resolutionCode);
                request.AddParameter("start", startDt.ToString("yyyy-M-d", CultureInfo.InvariantCulture));
                request.AddParameter("assetType", 1);
                request.AddParameter("page", page);
                request.AddParameter("pageSize", pageSize);

                Console.WriteLine($"[INFO] Fetching page {page}: {underlying.Symbol}, {startDt:yyyy-MM-dd} ~ {endDt:yyyy-MM-dd}, {resolutionLevel}");

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
