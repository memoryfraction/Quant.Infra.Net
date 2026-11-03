using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// Binance 现货日线（免费、免 API key、只读公开行情）：BTCUSDT / ETHUSDT / SOLUSDT …
/// Binance spot daily klines (free, no API key, public read-only market data): BTCUSDT, ETHUSDT, SOLUSDT, …
/// </summary>
/// <remarks>
/// 走公开行情镜像 <c>data-api.binance.vision</c>（只提供行情、不涉及账户，美国 IP 也可访问）。
/// 符号自动规范化：BTC → BTCUSDT，BTC-USD / BTC/USDT → BTCUSDT。每次最多 1000 根，自动分页。
/// Uses the public market-data mirror <c>data-api.binance.vision</c> (market data only, no account access; reachable from
/// US IPs). Symbols are normalized: BTC → BTCUSDT, BTC-USD / BTC/USDT → BTCUSDT. Paginates 1000 bars per call.
/// </remarks>
public sealed class BinanceSourceDataService : IMcpSourceDataService
{
    private const string BaseUrl = "https://data-api.binance.vision/api/v3/klines";
    private readonly HttpClient _http;

    /// <summary>构造 / Constructor.</summary>
    public BinanceSourceDataService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <inheritdoc />
    public string Provider => "Binance";

    /// <summary>
    /// 符号规范化 / Normalizes a crypto symbol to Binance format.
    /// </summary>
    public static string Normalize(string symbol)
    {
        var s = symbol.Trim().ToUpperInvariant().Replace("-", "").Replace("/", "").Replace("_", "");
        if (s.EndsWith("USD", StringComparison.Ordinal) && !s.EndsWith("BUSD", StringComparison.Ordinal))
        {
            s += "T"; // BTCUSD → BTCUSDT
        }

        var quotes = new[] { "USDT", "USDC", "FDUSD", "BTC", "ETH", "BNB", "EUR", "TRY" };
        return quotes.Any(q => s.Length > q.Length && s.EndsWith(q, StringComparison.Ordinal)) ? s : s + "USDT";
    }

    /// <inheritdoc />
    public async Task<Ohlcvs> DownloadDailyAsync(string symbol, DateTime start, DateTime end)
    {
        var pair = Normalize(symbol);
        var startMs = new DateTimeOffset(DateTime.SpecifyKind(start.Date, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var endMs = new DateTimeOffset(DateTime.SpecifyKind(end.Date.AddDays(1), DateTimeKind.Utc)).ToUnixTimeMilliseconds() - 1;

        var ohlcvs = new Ohlcvs { Symbol = symbol.Trim().ToUpperInvariant(), OhlcvSet = new HashSet<Ohlcv>() };
        var cursor = startMs;
        for (var page = 0; page < 50 && cursor <= endMs; page++)
        {
            var url = $"{BaseUrl}?symbol={pair}&interval=1d&startTime={cursor}&endTime={endMs}&limit=1000";
            using var resp = await _http.GetAsync(url).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                throw new ArgumentException($"Binance: request for '{pair}' failed (HTTP {(int)resp.StatusCode}): {body}. Use pairs like BTCUSDT / ETHUSDT.");
            }

            var added = ParseInto(ohlcvs, body, out var lastOpenMs);
            if (added == 0) break;
            cursor = lastOpenMs + 86_400_000L;
            if (added < 1000) break;
        }

        if (ohlcvs.OhlcvSet.Count > 0)
        {
            ohlcvs.StartDateTimeUtc = ohlcvs.OhlcvSet.Min(b => b.OpenDateTime);
            ohlcvs.EndDateTimeUtc = ohlcvs.OhlcvSet.Max(b => b.CloseDateTime);
        }

        return ohlcvs;
    }

    /// <summary>
    /// 解析 klines JSON 数组（公开以便离线单测）/ Parses a klines JSON array (public for offline unit tests).
    /// </summary>
    public static int ParseInto(Ohlcvs target, string json, out long lastOpenMs)
    {
        lastOpenMs = 0;
        using var doc = JsonDocument.Parse(json);
        var added = 0;
        foreach (var k in doc.RootElement.EnumerateArray())
        {
            var openMs = k[0].GetInt64();
            lastOpenMs = openMs;
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(openMs).UtcDateTime;
            var close = D(k[4]);
            target.OhlcvSet.Add(new Ohlcv
            {
                Symbol = target.Symbol,
                OpenDateTime = DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                CloseDateTime = DateTime.SpecifyKind(dt.AddDays(1), DateTimeKind.Utc),
                Open = D(k[1]),
                High = D(k[2]),
                Low = D(k[3]),
                Close = close,
                Volume = D(k[5]),
                AdjustedClose = close
            });
            added++;
        }

        return added;
    }

    private static decimal D(JsonElement e)
        => e.ValueKind == JsonValueKind.String
            ? decimal.Parse(e.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture)
            : e.GetDecimal();
}
