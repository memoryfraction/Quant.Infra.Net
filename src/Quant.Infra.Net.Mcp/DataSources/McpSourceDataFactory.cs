using System.Net.Http;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// 数据源工厂（SOLID: 单一职责 — 只负责"根据名字/配置选择哪个 Provider"）。
/// Data source factory (SOLID: Single Responsibility — picks a provider from a name / config).
/// </summary>
/// <remarks>
/// API key 解析优先级 / API key resolution order:
/// 1. 显式传入的 apiKey / explicit apiKey argument
/// 2. appsettings.json 的 <c>QuantInfraNet:DataSources:{Section}:ApiKey</c>
/// 3. 环境变量 / env var (<c>TIINGO_API_KEY</c>, <c>FINNHUB_API_KEY</c>, <c>FMP_API_KEY</c>, <c>TWELVEDATA_API_KEY</c>)
///
/// 免 key：Yahoo（股票/ETF/指数/外汇/港股/A股）、Binance（加密）、LocalFile。
/// No key: Yahoo (stocks/ETFs/indices/FX/HK/China A), Binance (crypto), LocalFile.
/// 放弃 Stooq：已被测试证明不稳定，工厂不暴露该 Provider。/ Stooq is intentionally not exposed (proven unstable).
/// </remarks>
public sealed class McpSourceDataFactory
{
    /// <summary>
    /// 受支持的 Provider 枚举 / Supported provider enum.
    /// </summary>
    public enum Provider { Finnhub, Fmp, TwelveData, LocalFile, Yahoo, Binance, Tiingo }

    /// <summary>该 Provider 是否需要 API key / Whether the provider needs an API key.</summary>
    public static bool RequiresKey(Provider p) => p is Provider.Finnhub or Provider.Fmp or Provider.TwelveData or Provider.Tiingo;

    /// <summary>
    /// 把用户输入的名字解析成 Provider（大小写与常见别名不敏感）；Demo 返回 null。
    /// Parses a user-supplied name into a Provider (case-insensitive, common aliases); returns null for Demo.
    /// </summary>
    /// <exception cref="ArgumentException">未知或被刻意放弃的数据源 / Unknown or intentionally unsupported source.</exception>
    public static Provider? Parse(string? raw)
    {
        var name = (raw ?? "").Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        return name switch
        {
            "" or "demo" or "synthetic" or "offline" => null,
            "yahoo" or "yahoofinance" or "yf" => Provider.Yahoo,
            "binance" or "crypto" => Provider.Binance,
            "tiingo" => Provider.Tiingo,
            "finnhub" => Provider.Finnhub,
            "fmp" or "financialmodelingprep" => Provider.Fmp,
            "twelvedata" or "12data" => Provider.TwelveData,
            "localfile" or "local" or "file" or "csv" => Provider.LocalFile,
            "stooq" => throw new ArgumentException(
                "Stooq is intentionally not exposed on the MCP surface — it has proven unstable. Use Yahoo (no key) or Tiingo (free key)."),
            _ => throw new ArgumentException(
                $"Unsupported dataSource '{raw}'. Supported: Demo | Yahoo | Binance | Tiingo | LocalFile | Finnhub | Fmp | TwelveData " +
                "(call list_data_sources for details).")
        };
    }

    /// <summary>
    /// 根据 Provider 创建数据源（网络源默认包一层磁盘缓存）。
    /// Creates a data source for the given provider (network sources are wrapped in a disk cache by default).
    /// </summary>
    /// <exception cref="ArgumentException">API key 缺失时抛出（fail-fast，不静默回退）。/ Thrown when an API key is missing.</exception>
    public IMcpSourceDataService Create(Provider provider, string? apiKey = null, string? localFilePath = null, HttpClient? httpClient = null, bool useCache = true)
    {
        if (provider == Provider.LocalFile)
        {
            var path = localFilePath ?? apiKey;
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("LocalFile requires localFilePath (CSV: date,open,high,low,close,volume[,adjusted_close] or JSON array).", nameof(localFilePath));
            return new LocalFileSourceDataService(path);
        }

        string? key = null;
        if (RequiresKey(provider))
        {
            key = apiKey ?? ResolveKey(provider);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(
                    $"API key is required for {provider}. Set env var {EnvVarName(provider)} (in your MCP client config \"env\" block), " +
                    $"or appsettings.json \"QuantInfraNet\" → \"DataSources\" → \"{SectionName(provider)}\" → \"ApiKey\". " +
                    $"Free key: {RegisterUrl(provider)}. No-key alternatives: Yahoo (stocks/ETFs) or Binance (crypto).",
                    nameof(apiKey));
        }

        IMcpSourceDataService svc = provider switch
        {
            Provider.Yahoo => new YahooSourceDataService(httpClient),
            Provider.Binance => new BinanceSourceDataService(httpClient),
            Provider.Tiingo => new TiingoSourceDataService(key!, httpClient),
            Provider.Finnhub => new FinnhubSourceDataService(key!, httpClient),
            Provider.Fmp => new FmpSourceDataService(key!, httpClient),
            Provider.TwelveData => new TwelveDataSourceDataService(key!, httpClient),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported provider.")
        };

        return useCache && CachingSourceDataService.Enabled ? new CachingSourceDataService(svc) : svc;
    }

    /// <summary>
    /// 从配置中解析 API key / Resolves API key from configuration.
    /// </summary>
    private static string? ResolveKey(Provider provider)
    {
        var fromFile = ReadKeyFromAppSettings(provider);
        if (!string.IsNullOrWhiteSpace(fromFile)) return fromFile;
        return Environment.GetEnvironmentVariable(EnvVarName(provider));
    }

    /// <summary>
    /// 从 appsettings.json 读取（自程序目录向上找 6 层）/ Reads from appsettings.json (walks up 6 levels).
    /// </summary>
    private static string? ReadKeyFromAppSettings(Provider provider)
    {
        try
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                var path = System.IO.Path.Combine(dir, "appsettings.json");
                if (System.IO.File.Exists(path))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("QuantInfraNet", out var qin) &&
                        qin.TryGetProperty("DataSources", out var ds) &&
                        ds.TryGetProperty(SectionName(provider), out var p) &&
                        p.TryGetProperty("ApiKey", out var k))
                        return k.GetString();
                }
                var parent = System.IO.Path.GetDirectoryName(dir.TrimEnd(System.IO.Path.DirectorySeparatorChar));
                if (parent is null || parent == dir) break;
                dir = parent;
            }
        }
        catch { /* ignore — fall through to env vars */ }
        return null;
    }

    /// <summary>appsettings.json 的 section 名 / appsettings.json section name.</summary>
    private static string SectionName(Provider provider) => provider.ToString();

    /// <summary>环境变量名 / Environment variable name.</summary>
    public static string EnvVarName(Provider provider) => provider switch
    {
        Provider.Finnhub => "FINNHUB_API_KEY",
        Provider.Fmp => "FMP_API_KEY",
        Provider.Tiingo => "TIINGO_API_KEY",
        Provider.TwelveData => "TWELVEDATA_API_KEY",
        _ => ""
    };

    /// <summary>注册页 URL / Registration URL.</summary>
    public static string RegisterUrl(Provider provider) => provider switch
    {
        Provider.Finnhub => "https://finnhub.io/register",
        Provider.Fmp => "https://site.financialmodelingprep.com/developer/docs",
        Provider.Tiingo => "https://www.tiingo.com/account/api/token",
        Provider.TwelveData => "https://twelvedata.com/pricing",
        _ => ""
    };
}
