using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Runtime.DataSources;

namespace Quant.Infra.Net.Runtime.Tests.DataSources;

/// <summary>
/// R8 验收测试：Stooq 免费日线源（HttpClient + CsvHelper，无新 NuGet 依赖）。
/// 用假 <see cref="HttpMessageHandler"/> 伪造真实格式的 Stooq CSV 响应，自动化测试零真实网络请求。
/// R8 acceptance tests: the Stooq daily CSV source drives through a fake HttpMessageHandler
/// (real Stooq CSV shape) with zero real network requests in CI.
/// </summary>
[TestClass]
public class StooqDataSourceTests
{
    private static readonly DateTime Start = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime End = new DateTime(2024, 1, 5, 0, 0, 0, DateTimeKind.Unspecified);

    private const string ValidCsv =
        "Date,Open,High,Low,Close,Volume\r\n" +
        "2024-01-02,100.50,105.25,99.75,104.10,1234500\r\n" +
        "2024-01-03,104.10,106.80,103.20,105.55,987600\r\n" +
        "2024-01-04,105.55,107.30,104.90,106.00,1100000\r\n" +
        "2024-01-05,106.00,109.99,105.10,109.25,2048000\r\n";

    /// <summary>正常解析：4 行日线全部入集，首末两条 OHLCV 数值逐字段正确 / Happy path: 4 daily rows, first/last OHLCV values verified field by field.</summary>
    [TestMethod]
    public async Task Download_ValidCsv_Parses_All_Rows_With_Correct_Ohlc_Values()
    {
        var handler = new FakeStooqHandler(HttpStatusCode.OK, ValidCsv);
        var (source, client) = Source(handler);

        var ohlcvs = await source.DownloadOhlcvListAsync("AAPL", Start, End);

        Assert.AreEqual("AAPL", ohlcvs.Symbol);
        Assert.AreEqual(4, ohlcvs.OhlcvSet.Count);

        var ordered = ohlcvs.OhlcvSet.OrderBy(x => x.OpenDateTime).ToList();

        var first = ordered[0];
        Assert.AreEqual(new DateTime(2024, 1, 2), first.OpenDateTime);
        Assert.AreEqual(100.50m, first.Open);
        Assert.AreEqual(105.25m, first.High);
        Assert.AreEqual(99.75m, first.Low);
        Assert.AreEqual(104.10m, first.Close);
        Assert.AreEqual(1234500m, first.Volume);

        var last = ordered[^1];
        Assert.AreEqual(new DateTime(2024, 1, 5), last.OpenDateTime);
        Assert.AreEqual(106.00m, last.Open);
        Assert.AreEqual(109.99m, last.High);
        Assert.AreEqual(105.10m, last.Low);
        Assert.AreEqual(109.25m, last.Close);
        Assert.AreEqual(2048000m, last.Volume);

        // 请求形态：小写 symbol + .us + i=d，且携带显式 User-Agent（防 .NET 默认 UA 被拒）
        // Request shape: lower-cased symbol + .us + i=d, with an explicit User-Agent header.
        Assert.AreEqual("https://stooq.com/q/d/l/?s=aapl.us&i=d", handler.LastRequestUri?.ToString());
        Assert.IsNotNull(handler.LastUserAgent);
    }

    /// <summary>空响应：返回空 OhlcvSet，不抛异常 / Empty response: empty OhlcvSet, no exception.</summary>
    [TestMethod]
    public async Task Download_EmptyResponse_Returns_Empty_OhlcvSet_Without_Throwing()
    {
        var handler = new FakeStooqHandler(HttpStatusCode.OK, "");
        var (source, client) = Source(handler);

        var ohlcvs = await source.DownloadOhlcvListAsync("AAPL", Start, End);

        Assert.AreEqual("AAPL", ohlcvs.Symbol);
        Assert.IsNotNull(ohlcvs.OhlcvSet);
        Assert.AreEqual(0, ohlcvs.OhlcvSet.Count);
    }

    /// <summary>格式错误 CSV：抛出带清晰错误信息的异常（含标的与 URL），不暴露裸 CsvHelper 堆栈 / Malformed CSV: InvalidOperationException with a clear message (symbol + URL), no raw stack leak.</summary>
    [TestMethod]
    public async Task Download_MalformedCsv_Throws_Clear_Error()
    {
        var handler = new FakeStooqHandler(
            HttpStatusCode.OK,
            "Date,Open,High,Low,Close,Volume\r\n" +
            "not-a-date,abc,def,ghi,jkl,mno\r\n");
        var (source, client) = Source(handler);

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => source.DownloadOhlcvListAsync("AAPL", Start, End));

        StringAssert.Contains(ex.Message, "AAPL");
        StringAssert.Contains(ex.Message, "https://stooq.com/q/d/l/?s=aapl.us&i=d");
        StringAssert.Contains(ex.Message, "malformed");
    }

    /// <summary>HTTP 404：抛出带清晰错误信息（状态码 + URL）的异常 / HTTP 404: InvalidOperationException with status code + URL in the message.</summary>
    [TestMethod]
    public async Task Download_Http404_Throws_Clear_Error()
    {
        var handler = new FakeStooqHandler(HttpStatusCode.NotFound, "Not Found");
        var (source, client) = Source(handler);

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => source.DownloadOhlcvListAsync("AAPL", Start, End));

        StringAssert.Contains(ex.Message, "404");
        StringAssert.Contains(ex.Message, "https://stooq.com/q/d/l/?s=aapl.us&i=d");
        StringAssert.Contains(ex.Message, "AAPL");
    }

    private static (StooqTraditionalFinanceSourceDataService Source, HttpClient Client) Source(FakeStooqHandler handler)
    {
        var client = new HttpClient(handler);
        return (new StooqTraditionalFinanceSourceDataService(client), client);
    }

    /// <summary>
    /// 假 HTTP 层：按用例返回固定状态码/正文，并记录请求 URL 与 User-Agent；零真实网络。
    /// Fake HTTP layer: returns a fixed status/body per test case and records the request URL
    /// and User-Agent; no real network access.
    /// </summary>
    private sealed class FakeStooqHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public FakeStooqHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        /// <summary>最近一次请求 URI / URI of the last request.</summary>
        public Uri? LastRequestUri { get; private set; }

        /// <summary>最近一次请求的 User-Agent（null = 未携带）/ User-Agent of the last request (null = absent).</summary>
        public string? LastUserAgent { get; private set; }

        /// <summary>构造固定响应并记录请求 / Builds the canned response and records the request.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastUserAgent = request.Headers.TryGetValues("User-Agent", out var values)
                ? string.Join(" ", values)
                : null;

            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            };
            return Task.FromResult(response);
        }
    }
}
