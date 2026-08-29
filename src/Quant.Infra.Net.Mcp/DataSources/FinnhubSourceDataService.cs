using System.Globalization;
using System.Net.Http;
using Quant.Infra.Net.SourceData.Model;
using System.Text.Json;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// Finnhub 实现（免费层 60 calls/min，注册 https://finnhub.io/register）。
/// Finnhub implementation (free tier 60 calls/min).
/// </summary>
public sealed class FinnhubSourceDataService : RestSourceDataServiceBase
{
    public FinnhubSourceDataService(string apiKey, HttpClient? httpClient = null)
        : base("Finnhub", apiKey, httpClient) { }

    protected override async Task<List<Ohlcv>> FetchBarsAsync(string symbol, DateTime start, DateTime end)
    {
        var url = $"https://finnhub.io/api/v1/stock/candle?symbol={Uri.EscapeDataString(symbol)}" +
                  $"&resolution=D" +
                  $"&from={(int)start.ToUniversalTime().Ticks / TimeSpan.TicksPerSecond}" +
                  $"&to={(int)end.ToUniversalTime().Ticks / TimeSpan.TicksPerSecond}" +
                  $"&token={Uri.EscapeDataString(ApiKey)}";

        using var resp = await Http.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("s", out var status) && status.GetString() == "no_data")
            return new List<Ohlcv>();

        int[] t = root.GetProperty("t").Deserialize<int[]>() ?? Array.Empty<int>();
        double[] o = root.GetProperty("o").Deserialize<double[]>() ?? Array.Empty<double>();
        double[] h = root.GetProperty("h").Deserialize<double[]>() ?? Array.Empty<double>();
        double[] l = root.GetProperty("l").Deserialize<double[]>() ?? Array.Empty<double>();
        double[] c = root.GetProperty("c").Deserialize<double[]>() ?? Array.Empty<double>();
        double[] v = root.GetProperty("v").Deserialize<double[]>() ?? Array.Empty<double>();

        var bars = new List<Ohlcv>(t.Length);
        for (int i = 0; i < t.Length; i++)
        {
            var dt = DateTime.UnixEpoch.AddSeconds(t[i]);
            bars.Add(new Ohlcv
            {
                Symbol = symbol.ToUpperInvariant(),
                OpenDateTime = dt,
                CloseDateTime = dt.AddDays(1),
                Open = (decimal)o[i],
                High = (decimal)h[i],
                Low = (decimal)l[i],
                Close = (decimal)c[i],
                Volume = (decimal)v[i],
                AdjustedClose = (decimal)c[i]
            });
        }
        return bars.OrderBy(b => b.OpenDateTime).ToList();
    }
}
