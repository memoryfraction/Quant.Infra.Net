using System.Net.Http;
using System.Text.Json;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// Tiingo 日线（免费 key，复权 OHLC，美股/ETF 30 年历史，稳定性优于 Yahoo）。注册 https://www.tiingo.com
/// Tiingo end-of-day bars (free API key; adjusted OHLC; ~30 years of US stocks/ETFs; more stable than Yahoo).
/// </summary>
public sealed class TiingoSourceDataService : RestSourceDataServiceBase
{
    /// <summary>构造 / Constructor.</summary>
    public TiingoSourceDataService(string apiKey, HttpClient? httpClient = null)
        : base("Tiingo", apiKey, httpClient) { }

    /// <inheritdoc />
    protected override async Task<List<Ohlcv>> FetchBarsAsync(string symbol, DateTime start, DateTime end)
    {
        var url = $"https://api.tiingo.com/tiingo/daily/{Uri.EscapeDataString(symbol.Trim().ToLowerInvariant())}/prices" +
                  $"?startDate={start:yyyy-MM-dd}&endDate={end:yyyy-MM-dd}&token={Uri.EscapeDataString(ApiKey)}";
        using var resp = await Http.GetAsync(url).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Tiingo request for '{symbol}' failed (HTTP {(int)resp.StatusCode}): {body}");
        }

        return Parse(symbol, body);
    }

    /// <summary>解析 Tiingo JSON（公开以便离线单测）/ Parses Tiingo JSON (public for offline unit tests).</summary>
    public static List<Ohlcv> Parse(string symbol, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var sym = symbol.Trim().ToUpperInvariant();
        var bars = new List<Ohlcv>();
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var dt = DateTime.SpecifyKind(e.GetProperty("date").GetDateTime().Date, DateTimeKind.Utc);
            var adjClose = e.GetProperty("adjClose").GetDecimal();
            bars.Add(new Ohlcv
            {
                Symbol = sym,
                OpenDateTime = dt,
                CloseDateTime = dt.AddDays(1),
                Open = e.GetProperty("adjOpen").GetDecimal(),
                High = e.GetProperty("adjHigh").GetDecimal(),
                Low = e.GetProperty("adjLow").GetDecimal(),
                Close = adjClose,
                Volume = e.TryGetProperty("adjVolume", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m,
                AdjustedClose = adjClose
            });
        }

        return bars.OrderBy(b => b.OpenDateTime).ToList();
    }
}
