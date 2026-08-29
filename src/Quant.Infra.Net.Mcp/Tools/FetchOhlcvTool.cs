using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Quant.Infra.Net.Mcp.DataSources;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Mcp.Tools;

/// <summary>
/// MCP tool type: fetch_ohlcv — downloads daily OHLCV bars from the configured data provider
/// (Finnhub / FMP / TwelveData, or a custom <see cref="IMcpSourceDataService"/> for tests) and
/// returns at most 500 bars (hard cap) with a truncated flag when the window is larger.
/// </summary>
/// <remarks>
/// SOLID: the tool depends on the <see cref="IMcpSourceDataService"/> abstraction, not on any
/// concrete provider. The provider is chosen by <see cref="McpSourceDataFactory"/> from
/// appsettings.json / env vars, or by an explicit <paramref name="dataSource"/> arg.
///
/// 500-bar hard cap: MCP tool results must stay small enough for the client to display; a
/// full year of daily bars is ~250 bars, so 500 comfortably covers 2 years and never blows
/// the response budget. Callers that need more should split the window into smaller ranges.
/// </remarks>
[McpServerToolType]
public static class FetchOhlcvTool
{
    /// <summary>
    /// Hard cap on the number of bars returned per tool call (plan: 500).
    /// </summary>
    public const int MaxBars = 500;

    /// <summary>
    /// Fetch daily OHLCV bars for a symbol over a UTC window, capped at 500.
    /// </summary>
    [McpServerTool, Description(
        "Fetch daily OHLCV bars for a symbol over a UTC window. Returns at most 500 bars (hard cap). " +
        "When the window produces more than 500 bars, the response is truncated to the FIRST 500 " +
        "(oldest first) with truncated=true and the total bar count. Use a smaller window for more bars. " +
        "Data source: Demo (offline synthetic, no API key) or one of Finnhub / FMP / TwelveData " +
        "(real daily bars; requires a free API key in appsettings.json or an env var). " +
        "Read-only, no orders.")]
    public static async Task<string> FetchOhlcv(
        [Description("Symbol, e.g. AAPL, TSLA, MSFT.")]
        string symbol,
        [Description("Window start, UTC, e.g. 2024-01-01.")]
        string startDate,
        [Description("Window end, UTC, e.g. 2024-06-30.")]
        string endDate,
        [Description("Data source: Demo (default, offline) | Finnhub | Fmp | TwelveData (real; needs API key).")]
        string? dataSource = null,
        [Description("Optional: explicit API key override (not needed if already in appsettings.json or env var).")]
        string? apiKey = null,
        [Description("Optional: file path for LocalFile source (CSV or JSON). Absolute or relative to AppContext.BaseDirectory.")]
        string? localFilePath = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("symbol is required.", nameof(symbol));

        var (start, end) = ParseWindow(startDate, endDate);

        // 1) Pick the data source (abstraction; concrete impl chosen by config).
        IMcpSourceDataService source;
        string providerName;
        try
        {
            (source, providerName) = ResolveSource(dataSource, apiKey, localFilePath);
        }
        catch (Exception ex)
        {
            return ErrorJson("cannot resolve data source: " + ex.Message, symbol, start, end);
        }

        // 2) Download (real providers hit the network; Demo is offline).
        Ohlcvs ohlcvs;
        try
        {
            ohlcvs = await source.DownloadDailyAsync(symbol, start, end).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ErrorJson("download failed: " + ex.Message, symbol, start, end);
        }

        // 3) Sort ascending, then apply the 500-bar hard cap (keep oldest 500).
        var all = ohlcvs.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList();
        var truncated = all.Count > MaxBars;
        var returned = truncated ? all.Take(MaxBars).ToList() : all;

        // 4) Shape output.
        var bars = returned.Select(b => new
        {
            date = b.OpenDateTime.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            open = b.Open,
            high = b.High,
            low = b.Low,
            close = b.Close,
            volume = b.Volume,
            adjustedClose = b.AdjustedClose
        }).ToList();

        var payload = new
        {
            symbol = symbol.ToUpperInvariant(),
            provider = providerName,
            window = new { start, end },
            totalBars = all.Count,
            returnedBars = returned.Count,
            truncated,
            maxBars = MaxBars,
            bars
        };

        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    /// <summary>
    /// Resolve the data source abstraction from the (dataSource, apiKey) pair.
    /// Defaults to Demo (offline) when no provider is named.
    /// </summary>
    private static (IMcpSourceDataService, string) ResolveSource(string? dataSource, string? apiKey, string? localFilePath)
    {
        var name = (dataSource ?? "Demo").Trim();
        if (name.Equals("Demo", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("synthetic", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("offline", StringComparison.OrdinalIgnoreCase))
        {
            return (new DemoMcpSource(), "Demo");
        }

        McpSourceDataFactory.Provider provider;
        switch (name)
        {
            case "Finnhub" or "finnhub":
                provider = McpSourceDataFactory.Provider.Finnhub; break;
            case "Fmp" or "fmp" or "FMP":
                provider = McpSourceDataFactory.Provider.Fmp; break;
            case "TwelveData" or "twelvedata" or "12data":
                provider = McpSourceDataFactory.Provider.TwelveData; break;
            case "LocalFile" or "localfile" or "local" or "file":
                provider = McpSourceDataFactory.Provider.LocalFile; break;
            default:
                throw new ArgumentException(
                    $"Unsupported dataSource \x27{name}\x27. Supported: Demo | Finnhub | Fmp | TwelveData | LocalFile. Use localFilePath to specify the file path.");
        }

        var factory = new McpSourceDataFactory();
        return (factory.Create(provider, apiKey, localFilePath), provider.ToString());
    }

    /// <summary>
    /// Offline Demo source: deterministic synthetic bars (reuses the Runtime's DemoSyntheticSourceDataService
    /// shape by generating a simple sine-wave series; zero network).
    /// </summary>
    private sealed class DemoMcpSource : IMcpSourceDataService
    {
        public string Provider => "Demo";

        public Task<Ohlcvs> DownloadDailyAsync(string symbol, DateTime start, DateTime end)
        {
            var days = (int)Math.Max(1, (end.Date - start.Date).TotalDays + 1);
            var ohlcvs = new Ohlcvs
            {
                Symbol = symbol.ToUpperInvariant(),
                StartDateTimeUtc = start,
                EndDateTimeUtc = end,
                OhlcvSet = new HashSet<Ohlcv>()
            };
            for (int i = 0; i < days; i++)
            {
                var t = start.Date.AddDays(i);
                var baseP = 100.0 + 20.0 * Math.Sin(i / 20.0) + i * 0.05;
                ohlcvs.OhlcvSet.Add(new Ohlcv
                {
                    Symbol = symbol.ToUpperInvariant(),
                    OpenDateTime = t,
                    CloseDateTime = t.AddDays(1),
                    Open = (decimal)baseP,
                    High = (decimal)(baseP + 1.5),
                    Low = (decimal)(baseP - 1.5),
                    Close = (decimal)(baseP + 0.5),
                    Volume = 1_000_000m + (i % 7) * 100_000m,
                    AdjustedClose = (decimal)(baseP + 0.5)
                });
            }
            return Task.FromResult(ohlcvs);
        }
    }

    private static string ErrorJson(string message, string symbol, DateTime start, DateTime end)
    {
        var payload = new
        {
            symbol,
            window = new { start, end },
            error = message,
            bars = Array.Empty<object>()
        };
        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    private static (DateTime Start, DateTime End) ParseWindow(string startRaw, string endRaw)
    {
        var start = ParseDate(startRaw, "startDate");
        var end = ParseDate(endRaw, "endDate");
        if (end < start)
            throw new ArgumentException($"endDate ({end:u}) must be >= startDate ({start:u}).");
        return (start, end);
    }

    private static DateTime ParseDate(string raw, string fieldName)
    {
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            throw new ArgumentException($"{fieldName} '{raw}' is not a valid date (expected ISO 8601 or yyyy-MM-dd).");
        }
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}

