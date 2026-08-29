using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Mcp;

/// <summary>
/// 回测容器工厂：把"策略名 + 参数表 + 数据源"组装成一次完整回测。
/// Backtest container factory: assembles "strategy name + parameter table + data source" into a single full backtest.
/// </summary>
/// <remarks>
/// 数据源支持：
///   <list type="bullet">
///     <item>Demo —— 离线合成数据（默认，零网络、确定性）。</item>
///     <item>Custom —— 调用方传入的 <paramref name="customDataSource"/>（Finnhub / FMP / TwelveData / LocalFile 经
///       <see cref="Mcp.DataSources.McpRuntimeSourceAdapter"/> 适配而来）。当为 null 时运行时会 fail-fast。</item>
///   </list>
/// Data sources:
///   <list type="bullet">
///     <item>Demo — offline synthetic (default; zero network, deterministic).</item>
///     <item>Custom — a caller-supplied <paramref name="customDataSource"/> (Finnhub / FMP / TwelveData / LocalFile,
///       adapted via <see cref="Mcp.DataSources.McpRuntimeSourceAdapter"/>). The runtime fail-fasts when null.</item>
///   </list>
/// </remarks>
internal static class RuntimeBacktestFactory
{
    /// <summary>
    /// 执行一次回测，返回结果对象。
    /// Runs one backtest and returns the result object.
    /// </summary>
    /// <param name="strategy">策略名（如 MaCross / MeanReversion / PairTradingZScore）/ Strategy name.</param>
    /// <param name="parameters">策略参数表（Symbol/SymbolA/SymbolB/周期/阈值等）/ Strategy parameter table.</param>
    /// <param name="dataSource">数据源种类 / Data source kind (Demo or Custom).</param>
    /// <param name="customDataSource">Custom 种类使用的实例（Finnhub/FMP/TwelveData/LocalFile 适配后）/ Custom source instance.</param>
    /// <param name="startDate">窗口起点（UTC）/ Window start (UTC).</param>
    /// <param name="endDate">窗口终点（UTC，含）/ Window end (UTC, inclusive).</param>
    /// <param name="symbols">参与回测的符号（与参数表一致）/ Symbols under test.</param>
    /// <param name="initialEquityUsd">初始权益（USD）/ Initial equity (USD).</param>
    /// <param name="commissionBps">手续费（基点）/ Commission in basis points.</param>
    /// <param name="slippageBps">滑点（基点）/ Slippage in basis points.</param>
    /// <param name="warmupBars">预热 bar 数 / Warm-up bars.</param>
    /// <returns>回测结果（权益曲线/成交/事件/指标）/ The backtest result (curve/trades/events/metrics).</returns>
    /// <exception cref="ArgumentException">strategy 为空白或未知策略名 / Thrown when strategy is blank or unknown.</exception>
    public static async Task<Quant.Infra.Net.Backtest.Models.BacktestResult> RunAsync(
        string strategy,
        IReadOnlyDictionary<string, string> parameters,
        Quant.Infra.Net.Runtime.Models.DataSourceKind dataSource,
        ITraditionalFinanceSourceDataService? customDataSource,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<string> symbols,
        decimal initialEquityUsd = 10_000m,
        decimal commissionBps = 5m,
        decimal slippageBps = 2m,
        int warmupBars = 0)
    {
        if (string.IsNullOrWhiteSpace(strategy))
        {
            throw new ArgumentException("strategy must not be blank.", nameof(strategy));
        }

        var catalog = new Quant.Infra.Net.Runtime.Strategies.StrategyCatalog(
            new[] { typeof(QuantInfraNetMcpServer).Assembly });
        var descriptor = catalog.Resolve(strategy);

        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt =>
            {
                rt.RunMode = RunMode.Backtest;
                rt.DataSource = dataSource;
            },
            o =>
            {
                foreach (var (key, value) in parameters)
                {
                    o.Parameters[key] = value;
                }

                o.Parameters["Strategy"] = strategy;
            },
            b =>
            {
                b.InitialEquityUsd = initialEquityUsd;
                b.CommissionBps = commissionBps;
                b.SlippageBps = slippageBps;
                b.WarmupBars = warmupBars;
            },
            customDataSource: customDataSource,
            strategyAssemblies: new[] { typeof(QuantInfraNetMcpServer).Assembly });

        using var provider = services.BuildServiceProvider();

        var svc = provider.GetRequiredService<ITraditionalFinanceSourceDataService>();

        // 数据下载（Demo 确定性；Custom = Finnhub/FMP/TwelveData/LocalFile）
        var ohlcvs = await svc.DownloadOhlcvListAsync(symbols[0], startDate, endDate).ConfigureAwait(false);

        var perSymbol = new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            [symbols[0]] = ohlcvs.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList()
        };
        foreach (var extra in symbols.Skip(1))
        {
            var extraOhlcvs = await svc.DownloadOhlcvListAsync(extra, startDate, endDate).ConfigureAwait(false);
            perSymbol[extra] = extraOhlcvs.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList();
        }

        var data = new Quant.Infra.Net.Backtest.Data.HistoricalDataSet(perSymbol);
        return await provider.GetRequiredService<BacktestRunner>()
            .RunAsync(data, symbols)
            .ConfigureAwait(false);
    }
}
