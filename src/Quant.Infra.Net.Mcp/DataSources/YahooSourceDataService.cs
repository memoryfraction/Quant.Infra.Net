using System.Net;
using System.Net.Http;
using System.Text.Json;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// Yahoo Finance 日线数据源（免费、免 API key）：美股 / ETF / 指数（^GSPC）/ 港股（0700.HK）/ A 股（600519.SS）/ 外汇（EURUSD=X）/ 期货（GC=F）。
/// Yahoo Finance daily bars (free, no API key): US stocks, ETFs, indices (^GSPC), HK (0700.HK), China A (600519.SS),
/// FX (EURUSD=X), futures (GC=F).
/// </summary>
/// <remarks>
/// <para>使用公开 chart 接口（v8）。OHLC 按 adjclose/close 比例做复权（含分红与拆股），因此回测是"总收益"口径。</para>
/// <para>Uses the public v8 chart endpoint. OHLC is back-adjusted by adjclose/close (dividends + splits) so backtests
/// measure total return.</para>
/// <para>非官方接口：Yahoo 可能限流或变更；遇到 429 会换备用主机重试一次。结果会被 <see cref="CachingSourceDataService"/> 缓存。</para>
/// <para>Unofficial endpoint: Yahoo may throttle or change it; a 429 retries once on the backup host. Results are cached by
/// <see cref="CachingSourceDataService"/>.</para>
/// </remarks>
public sealed class YahooSourceDataService : IMcpSourceDataService
{
    private static readonly string[] Hosts = { "https://query1.finance.yahoo.com", "https://query2.finance.yahoo.com" };
    private readonly HttpClient _http;

    /// <summary>构造 / Constructor.</summary>
    /// <param name="httpClient">可选 HttpClient（测试注入）/ Optional HttpClient (test injection).</param>
    public YahooSourceDataService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Quant.Infra.Net.Mcp/1.6)");
        }
    }

    /// <inheritdoc />
    public string Provider => "Yahoo";

    /// <inheritdoc />
    public async Task<Ohlcvs> DownloadDailyAsync(string symbol, DateTime start, DateTime end)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("symbol is required.", nameof(symbol));

        var p1 = new DateTimeOffset(DateTime.SpecifyKind(start.Date, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var p2 = new DateTimeOffset(DateTime.SpecifyKind(end.Date.AddDays(1), DateTimeKind.Utc)).ToUnixTimeSeconds();
        var path = $"/v8/finance/chart/{Uri.EscapeDataString(symbol.Trim())}?period1={p1}&period2={p2}&interval=1d&events=div%2Csplit&includeAdjustedClose=true";

        string? json = null;
        HttpStatusCode last = HttpStatusCode.OK;
        foreach (var host in Hosts)
        {
            using var resp = await _http.GetAsync(host + path).ConfigureAwait(false);
            last = resp.StatusCode;
            if (resp.IsSuccessStatusCode)
            {
                json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                break;
            }

            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ArgumentException($"Yahoo: unknown symbol '{symbol}'. Examples: AAPL, SPY, ^GSPC, 0700.HK, 600519.SS, BTC-USD, EURUSD=X.");
            }
        }

        if (json is null)
        {
            throw new HttpRequestException($"Yahoo request failed (HTTP {(int)last}). Yahoo may be rate-limiting; retry in a minute, or use Tiingo/Binance/LocalFile.");
        }

        return Parse(symbol, json);
    }

    /// <summary>
    /// 解析 chart JSON（公开以便离线单测）/ Parses chart JSON (public for offline unit tests).
    /// </summary>
    public static Ohlcvs Parse(string symbol, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var chart = doc.RootElement.GetProperty("chart");
        if (chart.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
        {
            var desc = err.TryGetProperty("description", out var d) ? d.GetString() : "unknown error";
            throw new ArgumentException($"Yahoo error for '{symbol}': {desc}");
        }

        var result = chart.GetProperty("result")[0];
        var sym = symbol.Trim().ToUpperInvariant();
        var ohlcvs = new Ohlcvs { Symbol = sym, OhlcvSet = new HashSet<Ohlcv>() };
        if (!result.TryGetProperty("timestamp", out var ts) || ts.ValueKind != JsonValueKind.Array)
        {
            return ohlcvs; // no bars in window
        }

        long gmtOffset = 0;
        if (result.TryGetProperty("meta", out var meta) && meta.TryGetProperty("gmtoffset", out var g) && g.ValueKind == JsonValueKind.Number)
        {
            gmtOffset = g.GetInt64();
        }

        var quote = result.GetProperty("indicators").GetProperty("quote")[0];
        JsonElement? adj = null;
        if (result.GetProperty("indicators").TryGetProperty("adjclose", out var adjArr) && adjArr.GetArrayLength() > 0)
        {
            adj = adjArr[0].GetProperty("adjclose");
        }

        var o = quote.GetProperty("open");
        var h = quote.GetProperty("high");
        var l = quote.GetProperty("low");
        var c = quote.GetProperty("close");
        var v = quote.GetProperty("volume");

        var seen = new HashSet<DateTime>();
        for (var i = 0; i < ts.GetArrayLength(); i++)
        {
            var close = Num(c, i);
            if (close is null or <= 0) continue; // holidays / halted days come back as null

            var adjClose = adj.HasValue ? Num(adj.Value, i) ?? close.Value : close.Value;
            var ratio = adjClose / close.Value;
            // 交易所本地日期 → UTC 00:00，保证不同市场/数据源的时间轴可对齐。
            // Exchange-local date → 00:00 UTC so timelines align across markets/providers.
            var date = DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64() + gmtOffset).UtcDateTime.Date;
            if (!seen.Add(date)) continue;

            var open = Num(o, i) ?? close.Value;
            var high = Num(h, i) ?? Math.Max(open, close.Value);
            var low = Num(l, i) ?? Math.Min(open, close.Value);
            ohlcvs.OhlcvSet.Add(new Ohlcv
            {
                Symbol = sym,
                OpenDateTime = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                CloseDateTime = DateTime.SpecifyKind(date.AddDays(1), DateTimeKind.Utc),
                Open = (decimal)(open * ratio),
                High = (decimal)(high * ratio),
                Low = (decimal)(low * ratio),
                Close = (decimal)adjClose,
                Volume = (decimal)(Num(v, i) ?? 0),
                AdjustedClose = (decimal)adjClose
            });
        }

        if (ohlcvs.OhlcvSet.Count > 0)
        {
            ohlcvs.StartDateTimeUtc = ohlcvs.OhlcvSet.Min(b => b.OpenDateTime);
            ohlcvs.EndDateTimeUtc = ohlcvs.OhlcvSet.Max(b => b.CloseDateTime);
        }

        return ohlcvs;
    }

    private static double? Num(JsonElement arr, int i)
    {
        if (i >= arr.GetArrayLength()) return null;
        var e = arr[i];
        return e.ValueKind == JsonValueKind.Number ? e.GetDouble() : null;
    }
}
