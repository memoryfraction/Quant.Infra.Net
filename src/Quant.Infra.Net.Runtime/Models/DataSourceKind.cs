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
    Custom = 4
}
