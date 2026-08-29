using System.Globalization;
using System.Net.Http;
using Quant.Infra.Net.SourceData.Model;
using System.Text.Json;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// TwelveData 实现（免费层 800 credits/day，注册 https://twelvedata.com/pricing）。
/// TwelveData implementation (free tier 800 credits/day).
/// </summary>
public sealed class TwelveDataSourceDataService : RestSourceDataServiceBase
{
    public TwelveDataSourceDataService(string apiKey, HttpClient? httpClient = null)
        : base("TwelveData", apiKey, httpClient) { }

    protected override async Task<List<Ohlcv>> FetchBarsAsync(string symbol, DateTime start, DateTime end)
    {
        var url = $"https://api.twelvedata.com/time_series?symbol={Uri.EscapeDataString(symbol)}" +
                  $"&interval=1day&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}" +
                  $"&apikey={Uri.EscapeDataString(ApiKey)}";

        using var resp = await Http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("status", out var st) && st.GetString() == "error")
        {
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "unknown error";
            throw new InvalidOperationException($"TwelveData API error: {msg}");
        }

        if (!root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
            return new List<Ohlcv>();

        var bars = new List<Ohlcv>();
        foreach (var item in values.EnumerateArray())
        {
            var dt = DateTime.Parse(item.GetProperty("datetime").GetString() ?? "",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var ohlc = new Ohlcv
            {
                Symbol = symbol.ToUpperInvariant(),
                OpenDateTime = dt,
                CloseDateTime = dt.AddDays(1),
                Open = (decimal)(item.TryGetProperty("open", out var op) ? op.GetDouble() : 0),
                High = (decimal)(item.TryGetProperty("high", out var hp) ? hp.GetDouble() : 0),
                Low = (decimal)(item.TryGetProperty("low", out var lp) ? lp.GetDouble() : 0),
                Close = (decimal)(item.TryGetProperty("close", out var cp) ? cp.GetDouble() : 0),
                Volume = (decimal)(item.TryGetProperty("volume", out var vp) ? vp.GetDouble() : 0)
            };
            bars.Add(ohlc);
        }
        return bars.OrderBy(b => b.OpenDateTime).ToList();
    }
}
