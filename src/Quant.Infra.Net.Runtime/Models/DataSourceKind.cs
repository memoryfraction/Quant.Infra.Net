namespace Quant.Infra.Net.Runtime.Models;

/// <summary>数据源种类（设计 §7.7）/ Data source kind (design section 7.7).</summary>
public enum DataSourceKind
{
    /// <summary>
    /// 离线合成数据（默认，零网络，用于 Demo/CI）。
    /// Offline synthetic data (default; zero network; for demos/CI).
    /// </summary>
    Demo = 0,

    /// <summary>
    /// Yahoo Finance（核心库 TraditionalFinanceSourceDataService + pythonnet/Yahoo Chart API）。
    /// Yahoo Finance (core library's TraditionalFinanceSourceDataService + pythonnet/Yahoo Chart API).
    /// </summary>
    Yahoo = 1,

    /// <summary>
    /// 本地 CSV（核心库文件读取路径，IHistoricalDataSourceService 缺省为 HistoricalDataSourceServiceCsv）。
    /// Local CSV (core library's file-based path; IHistoricalDataSourceService defaults to HistoricalDataSourceServiceCsv).
    /// </summary>
    Csv = 2,

    /// <summary>
    /// Binance K 线接口（走 IBinanceUsdFutureService.GetOhlcvListAsync，只读）。
    /// Binance klines (via IBinanceUsdFutureService.GetOhlcvListAsync, read-only).
    /// </summary>
    Binance = 3,

    /// <summary>
    /// 用户自定义实现（由调用方传入的 customDataSource 提供）。
    /// User-supplied implementation (provided by the caller's customDataSource instance).
    /// </summary>
    Custom = 4,

    /// <summary>
    /// stooq.com 免费日线 CSV，无需 API Key，社区数据源非 SLA 保证 /
    /// Free daily CSV from stooq.com, no API key, community data source with no SLA guarantee.
    /// </summary>
    Stooq = 5,

    /// <summary>
    /// Alpaca Market Data（核心库 AlpacaClient，IEX 免费层，官方维护 SDK，需要免费 API Key）/
    /// Alpaca Market Data (core library's AlpacaClient, free IEX tier, officially maintained SDK,
    /// requires a free API key). Recommended default for real (non-demo) historical data — see
    /// RuntimeOptions.AlpacaApiKey / AlpacaApiSecret.
    /// </summary>
    Alpaca = 6
}
