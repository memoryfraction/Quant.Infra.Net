using System.Net.Http.Headers;
using Quant.Infra.Net.SourceData.Model;
using System.Text.Json;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// REST 数据源基类（SOLID: 模板方法 + 单一职责）。
/// REST data source base (SOLID: Template Method + Single Responsibility).
/// </summary>
/// <remarks>
/// 子类只负责：拼 URL、把 JSON 解析成 <see cref="Ohlcv"/> 列表。
/// 基类负责：HTTP 调用、User-Agent、错误处理、组装 <see cref="Ohlcvs"/>。
/// </remarks>
public abstract class RestSourceDataServiceBase : IMcpSourceDataService
{
    protected RestSourceDataServiceBase(string provider, string apiKey, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException($"API key is required for {provider}.", nameof(apiKey));

        Provider = provider;
        ApiKey = apiKey;
        Http = httpClient ?? new HttpClient();
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Quant.Infra.Net.Mcp/1.0");
    }

    /// <inheritdoc />
    public string Provider { get; }

    /// <summary>API key（子类用于拼 URL）/ API key (used by subclasses for URL building).</summary>
    protected string ApiKey { get; }

    /// <summary>共享 HttpClient / Shared HttpClient.</summary>
    protected HttpClient Http { get; }

    /// <inheritdoc />
    public async Task<Ohlcvs> DownloadDailyAsync(string symbol, DateTime start, DateTime end)
    {
        var bars = await FetchBarsAsync(symbol, start, end);
        var ohlcvs = new Ohlcvs
        {
            Symbol = symbol.ToUpperInvariant(),
            StartDateTimeUtc = bars.Count > 0 ? bars[0].OpenDateTime : start,
            EndDateTimeUtc = bars.Count > 0 ? bars[^1].CloseDateTime : end
        };
        foreach (var bar in bars)
            ohlcvs.OhlcvSet.Add(bar);
        return ohlcvs;
    }

    /// <summary>
    /// 子类实现：拼 URL 并解析 JSON 为 K 线列表 / Subclasses implement: build URL and parse JSON into bars.
    /// </summary>
    protected abstract Task<List<Ohlcv>> FetchBarsAsync(string symbol, DateTime start, DateTime end);
}
