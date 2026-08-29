using System.Net.Http;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// 数据源工厂（SOLID: 单一职责 — 只负责"根据配置选择哪个 Provider"）。
/// Data source factory (SOLID: Single Responsibility — picks a provider from config).
/// </summary>
/// <remarks>
/// 配置来源优先级：
/// 1. 显式传入的 <paramref name="provider"/> + <paramref name="apiKey"/>
/// 2. appsettings.json 的 <c>QuantInfraNet:DataSources:{Finnhub|Fmp|TwelveData}:ApiKey</c>
/// 3. 环境变量 <c>FINNHUB_API_KEY</c> / <c>FMP_API_KEY</c> / <c>TWELVEDATA_API_KEY</c>
///
/// 放弃 Stooq：已被测试证明不稳定，工厂不暴露该 Provider。
/// </remarks>
public sealed class McpSourceDataFactory
{
    /// <summary>
    /// 受支持的 Provider 枚举 / Supported provider enum.
    /// </summary>
    public enum Provider { Finnhub, Fmp, TwelveData, LocalFile }

    /// <summary>
    /// 根据 Provider + 显式 key 创建数据源 / Creates a data source for the given provider + explicit key.
    /// </summary>
    /// <exception cref="ArgumentException">API key 缺失时抛出（fail-fast，不静默回退）。/ Thrown when API key is missing.</exception>
    public IMcpSourceDataService Create(Provider provider, string? apiKey = null, string? localFilePath = null, HttpClient? httpClient = null)
    {
        var key = provider == Provider.LocalFile
            ? (localFilePath ?? apiKey ?? ResolveKey(provider))
            : (apiKey ?? ResolveKey(provider));
        if (string.IsNullOrWhiteSpace(key) && provider != Provider.LocalFile)
            throw new ArgumentException(
                $"API key is required for {provider}. Add it to appsettings.json under " +
                $"\"QuantInfraNet\" → \"DataSources\" → \"{SectionName(provider)}\" → \"ApiKey\", " +
                $"or set env var {EnvVarName(provider)}. Free tier: {RegisterUrl(provider)}.",
                nameof(apiKey));

        return provider switch
        {
            Provider.Finnhub => new FinnhubSourceDataService(key!, httpClient),
            Provider.Fmp => new FmpSourceDataService(key!, httpClient),
            Provider.TwelveData => new TwelveDataSourceDataService(key!, httpClient),
            Provider.LocalFile => new LocalFileSourceDataService(key!),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported provider.")
        };
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
    /// 从 appsettings.json 读取 / Reads from appsettings.json.
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
                var parent = System.IO.Path.GetDirectoryName(dir);
                if (parent is null || parent == dir) break;
                dir = parent;
            }
        }
        catch { /* ignore — fall through to env vars */ }
        return null;
    }

    /// <summary>
    /// appsettings.json 的 section 名 / appsettings.json section name.
    /// </summary>
    private static string SectionName(Provider provider) => provider switch
    {
        Provider.Finnhub => "Finnhub",
        Provider.Fmp => "Fmp",
        _ => "TwelveData"
    };

    /// <summary>
    /// 环境变量名 / Environment variable name.
    /// </summary>
    public static string EnvVarName(Provider provider) => provider switch
    {
        Provider.Finnhub => "FINNHUB_API_KEY",
        Provider.Fmp => "FMP_API_KEY",
        _ => "TWELVEDATA_API_KEY"
    };

    /// <summary>
    /// 注册页 URL / Registration URL.
    /// </summary>
    public static string RegisterUrl(Provider provider) => provider switch
    {
        Provider.Finnhub => "https://finnhub.io/register",
        Provider.Fmp => "https://financialmodelingprep.com/subscription",
        _ => "https://twelvedata.com/pricing"
    };
}



