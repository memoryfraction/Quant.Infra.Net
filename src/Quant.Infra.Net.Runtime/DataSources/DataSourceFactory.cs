using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.SourceData.Service.Historical;

namespace Quant.Infra.Net.Runtime.DataSources;

/// <summary>
/// 数据源工厂：把 <see cref="Models.DataSourceKind"/> 解析为具体的 <see cref="ITraditionalFinanceSourceDataService"/> 实现（设计 §7.7）。
/// Data source factory: resolves a DataSourceKind into a concrete ITraditionalFinanceSourceDataService implementation (design section 7.7).
/// </summary>
/// <remarks>
/// Demo = 离线合成源（本程序集）；Yahoo/Csv = 核心库 TraditionalFinanceSourceDataService（不同的 IHistoricalDataSourceService 依赖）；
/// Binance = 本程序集的 K 线适配；Custom = 调用方实例（必须提供）。
/// Demo = offline synthetic source (this assembly); Yahoo/Csv = core TraditionalFinanceSourceDataService
/// (with different IHistoricalDataSourceService dependencies); Binance = this assembly's kline adapter;
/// Custom = the caller-supplied instance (required).
/// </remarks>
public static class DataSourceFactory
{
    /// <summary>
    /// 按数据源种类创建 <see cref="ITraditionalFinanceSourceDataService"/> 实例。
    /// Creates an ITraditionalFinanceSourceDataService for the given kind.
    /// </summary>
    /// <param name="kind">数据源种类 / Data source kind.</param>
    /// <param name="serviceProvider">统一容器（Csv/Yahoo 种类缺省 HistoricalDataSourceServiceCsv；Binance 种类需 IBinanceUsdFutureService）/ Unified container (Csv/Yahoo default to HistoricalDataSourceServiceCsv; Binance requires IBinanceUsdFutureService).</param>
    /// <param name="customDataSource">Custom 种类的自定义实例（其他种类忽略）/ Custom kind instance (ignored otherwise).</param>
    /// <param name="alpacaApiKey">Alpaca 种类所需的 API Key（其他种类忽略）/ API key required by the Alpaca kind (ignored otherwise).</param>
    /// <param name="alpacaApiSecret">Alpaca 种类所需的 API Secret（其他种类忽略）/ API secret required by the Alpaca kind (ignored otherwise).</param>
    /// <returns>对应种类的数据源实例 / The data source instance for the kind.</returns>
    /// <exception cref="ArgumentNullException">serviceProvider 为 null 时抛出 / Thrown when serviceProvider is null.</exception>
    /// <exception cref="ArgumentException">kind 为 Custom 且 customDataSource 未提供，或 kind 为 Alpaca 且凭据缺失时抛出（fail-fast，不静默回退）/ Thrown when kind is Custom without customDataSource, or Alpaca without credentials (fail-fast, never silent fallback).</exception>
    /// <exception cref="InvalidOperationException">未知种类 / Unknown kind.</exception>
    public static ITraditionalFinanceSourceDataService Create(
        Models.DataSourceKind kind,
        IServiceProvider serviceProvider,
        ITraditionalFinanceSourceDataService? customDataSource,
        string? alpacaApiKey = null,
        string? alpacaApiSecret = null)
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        return kind switch
        {
            Models.DataSourceKind.Demo => new DemoSyntheticSourceDataService(),

            Models.DataSourceKind.Yahoo => new TraditionalFinanceSourceDataService(
                serviceProvider.GetService<IHistoricalDataSourceService>() ?? new HistoricalDataSourceServiceCsv()),

            Models.DataSourceKind.Csv => new TraditionalFinanceSourceDataService(
                serviceProvider.GetService<IHistoricalDataSourceService>() ?? new HistoricalDataSourceServiceCsv()),

            Models.DataSourceKind.Binance => new BinanceKlineSourceDataService(
                serviceProvider.GetRequiredService<IBinanceUsdFutureService>()),

            Models.DataSourceKind.Stooq => new StooqTraditionalFinanceSourceDataService(),

            Models.DataSourceKind.Alpaca => !string.IsNullOrWhiteSpace(alpacaApiKey) && !string.IsNullOrWhiteSpace(alpacaApiSecret)
                ? new AlpacaTraditionalFinanceSourceDataService(alpacaApiKey!, alpacaApiSecret!)
                : throw new ArgumentException(
                    "DataSourceKind.Alpaca requires RuntimeOptions.AlpacaApiKey/AlpacaApiSecret " +
                    "(free tier: sign up at https://alpaca.markets — fail-fast by design, never silently falls back).",
                    nameof(alpacaApiKey)),

            Models.DataSourceKind.Custom => customDataSource ?? throw new ArgumentException(
                "DataSourceKind.Custom requires a custom ITraditionalFinanceSourceDataService instance " +
                "(pass it via AddQuantInfraNet / Create's customDataSource parameter).",
                nameof(customDataSource)),

            _ => throw new InvalidOperationException($"Unknown DataSourceKind: {kind}.")
        };
    }
}
