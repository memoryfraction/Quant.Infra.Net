using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Runtime.DataSources;

/// <summary>
/// Binance K 线数据源适配（Binance 数据种类）：把只读 K 线拉取适配到 <see cref="ITraditionalFinanceSourceDataService"/>
/// （设计 §7.7；管道内唯一被调用的成员是 DownloadOhlcvListAsync，转发到 IBinanceUsdFutureService.GetOhlcvListAsync）。
/// Binance kline adapter (Binance kind): adapts read-only kline fetching onto ITraditionalFinanceSourceDataService
/// (design section 7.7; the only pipeline-invoked member is DownloadOhlcvListAsync, forwarded to
/// IBinanceUsdFutureService.GetOhlcvListAsync).
/// </summary>
public sealed class BinanceKlineSourceDataService : ITraditionalFinanceSourceDataService
{
    private readonly IBinanceUsdFutureService _broker;

    /// <summary>
    /// 初始化 Binance K 线数据源。
    /// Initializes the Binance kline data source.
    /// </summary>
    /// <param name="broker">币安合约服务（只读 K 线路径，不得为 null）/ Binance futures service (read-only kline path; must not be null).</param>
    public BinanceKlineSourceDataService(IBinanceUsdFutureService broker)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    /// <summary>
    /// 下载 OHLCV：转发到 broker.GetOhlcvListAsync（只读）。
    /// Download OHLCV: forwards to broker.GetOhlcvListAsync (read-only).
    /// </summary>
    public async Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, Shared.Model.ResolutionLevel Period = Shared.Model.ResolutionLevel.Daily, Shared.Model.DataSource dataSource = Shared.Model.DataSource.YahooFinance)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("symbol must not be blank.", nameof(symbol));
        }

        return await _broker.GetOhlcvListAsync(symbol, startDt, endDt, Period).ConfigureAwait(false);
    }

    /// <summary>
    /// 同步每日数据：与 Download 同源（只读 K 线）。
    /// Begin syncing daily data: same read-only kline path as Download.
    /// </summary>
    public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, Shared.Model.ResolutionLevel Period = Shared.Model.ResolutionLevel.Daily)
        => DownloadOhlcvListAsync(symbol, startDt, endDt, Period);

    /// <summary>
    /// 文件读取不适用（K 线源无文件概念）。
    /// File-based read is not applicable (kline source has no file notion).
    /// </summary>
    public Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename)
        => throw new NotSupportedException("Binance kline source does not support file-based reads.");

    /// <summary>
    /// 文件保存不适用（K 线源无文件概念）。
    /// File-based save is not applicable (kline source has no file notion).
    /// </summary>
    public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName)
        => throw new NotSupportedException("Binance kline source does not support file-based saves.");

    /// <summary>
    /// SP500 列表不适用（K 线源不提供股票列表）。
    /// S&P 500 list is not applicable (kline source provides no equity list).
    /// </summary>
    public Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500)
        => throw new NotSupportedException("Binance kline source does not provide S&P 500 symbols.");
}
