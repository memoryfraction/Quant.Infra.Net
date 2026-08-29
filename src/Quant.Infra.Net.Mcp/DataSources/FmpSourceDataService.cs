using System.Globalization;
using System.Net.Http;
using Quant.Infra.Net.SourceData.Model;
using System.Text.Json;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// FMP (Financial Modeling Prep) 实现（免费层 250 requests/day，注册 https://financialmodelingprep.com/subscription）。
/// FMP implementation (free tier 250 requests/day).
/// </summary>
public sealed class FmpSourceDataService : RestSourceDataServiceBase
{
    public FmpSourceDataService(string apiKey, HttpClient? httpClient = null)
        : base("Fmp", apiKey, httpClient) { }

    protected override async Task<List<Ohlcv>> FetchBarsAsync(string symbol, DateTime start, DateTime end)
    {
        var url = $"https://financialmodelingprep.com/api/v3/historical-price-full/{Uri.EscapeDataString(symbol)}" +
                  $"?from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}" +
                  $"&apikey={Uri.EscapeDataString(ApiKey)}";

        using var resp = await Http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var arr = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.TryGetProperty("historical", out var h) ? h
            : throw new InvalidOperationException("Unexpected FMP response format.");

        var bars = new List<Ohlcv>();
        foreach (var item in arr.EnumerateArray())
        {
            var date = item.GetProperty("date").GetString() ?? "";
            var dt = DateTime.Parse(date, CultureInfo.InvariantCulture,
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
            if (item.TryGetProperty("adjClose", out var ac) && ac.ValueKind != JsonValueKind.Null)
                ohlc.AdjustedClose = (decimal)ac.GetDouble();
            bars.Add(ohlc);
        }
        return bars.OrderBy(b => b.OpenDateTime).ToList();
    }
}
