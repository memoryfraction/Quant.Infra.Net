using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// 适配器：把 MCP 层数据源（Finnhub / FMP / TwelveData / LocalFile）适配成运行时的
/// <see cref="ITraditionalFinanceSourceDataService"/>，这样 <c>run_backtest</c> 就能通过
/// <c>DataSourceKind.Custom</c> 使用真实/本地数据源，而无需修改受保护的 Runtime 代码。
/// Adapter: bridges an <see cref="IMcpSourceDataService"/> (Finnhub / FMP / TwelveData / LocalFile) into the
/// runtime's <see cref="ITraditionalFinanceSourceDataService"/> so <c>run_backtest</c> can use real/local data
/// via <c>DataSourceKind.Custom</c> without touching the protected Runtime code.
/// </summary>
/// <remarks>
/// 只实现回测路径实际用到的 <c>DownloadOhlcvListAsync</c>；其余方法抛出
/// <see cref="NotSupportedException"/>（MCP 回测不需要 BeginSync / 保存 / 标普500 列表）。
/// Only <c>DownloadOhlcvListAsync</c> is implemented (the one the backtest path calls); the rest throw
/// <see cref="NotSupportedException"/> (BeginSync / save / SP-500 list are not used by MCP backtests).
/// </remarks>
public sealed class McpRuntimeSourceAdapter : ITraditionalFinanceSourceDataService
{
    private readonly IMcpSourceDataService _inner;

    /// <summary>用底层 MCP 数据源构造适配器 / Construct with an underlying MCP data source.</summary>
    public McpRuntimeSourceAdapter(IMcpSourceDataService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>底层数据源（便于诊断）/ The underlying data source (for diagnostics).</summary>
    public string Provider => _inner.Provider;

    /// <inheritdoc />
    public Task<Ohlcvs> DownloadOhlcvListAsync(
        string symbol, DateTime startDt, DateTime endDt,
        ResolutionLevel Period = ResolutionLevel.Daily, DataSource dataSource = DataSource.YahooFinance)
        => _inner.DownloadDailyAsync(symbol, startDt, endDt);

    /// <inheritdoc />
    public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(
        string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, ResolutionLevel Period = ResolutionLevel.Daily)
        => throw new NotSupportedException($"{Provider} (MCP adapter) does not support BeginSyncSourceDailyDataAsync.");

    /// <inheritdoc />
    public Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename)
        => throw new NotSupportedException($"{Provider} (MCP adapter) does not support GetOhlcvListAsync.");

    /// <inheritdoc />
    public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName)
        => throw new NotSupportedException($"{Provider} (MCP adapter) does not support SaveOhlcvListAsync.");

    /// <inheritdoc />
    public Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500)
        => throw new NotSupportedException($"{Provider} (MCP adapter) does not support GetSp500SymbolsAsync.");
}
