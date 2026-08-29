using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Mcp;

/// <summary>
/// 回测容器工厂：把"策略名 + 参数表 + 数据源种类"组装成一次完整回测（零网络，仅离线数据）。
/// Backtest container factory: assembles "strategy name + parameter table + data source kind" into a
/// single full backtest run (offline data only, zero network).
/// </summary>
/// <remarks>
/// 实现要点：
/// 1. 通过 <see cref="Quant.Infra.Net.Runtime.Strategies.StrategyCatalog"/>（反射）按名字解析
///    <see cref="Quant.Infra.Net.Orchestration.Abstractions.ISignalGenerator"/> 描述符；
/// 2. 调 <c>AddQuantInfraNet(...)</c> 的 Backtest 分支（D1 机制：BacktestBrokerService + BacktestRunner）；
/// 3. 数据来自 <see cref="ITraditionalFinanceSourceDataService.DownloadOhlcvListAsync"/>；
/// 4. 回测窗口结束到当前 UTC 之前（Demo 源为确定性序列，与窗口无关；Stooq 源真实日线）。
/// </remarks>
internal static class RuntimeBacktestFactory
{
    /// <summary>
    /// 执行一次回测，返回结果对象。
    /// Runs one backtest and returns the result object.
    /// </summary>
    /// <param name="strategy">策略名（如 MaCross / MeanReversion / PairTradingZScore）/ Strategy name.</param>
    /// <param name="parameters">策略参数表（Symbol/SymbolA/SymbolB/周期/阈值等）/ Strategy parameter table.</param>
    /// <param name="dataSource">数据源种类 / Data source kind.</param>
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
            strategyAssemblies: new[] { typeof(QuantInfraNetMcpServer).Assembly });

        using var provider = services.BuildServiceProvider();

        // 数据下载（离线源确定性；Stooq 源真实日线）
        var ohlcvs = await provider.GetRequiredService<ITraditionalFinanceSourceDataService>()
            .DownloadOhlcvListAsync(symbols[0], startDate, endDate)
            .ConfigureAwait(false);

        var perSymbol = new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            [symbols[0]] = ohlcvs.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList()
        };
        foreach (var extra in symbols.Skip(1))
        {
            var extraOhlcvs = await provider.GetRequiredService<ITraditionalFinanceSourceDataService>()
                .DownloadOhlcvListAsync(extra, startDate, endDate)
                .ConfigureAwait(false);
            perSymbol[extra] = extraOhlcvs.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList();
        }

        var data = new Quant.Infra.Net.Backtest.Data.HistoricalDataSet(perSymbol);
        return await provider.GetRequiredService<BacktestRunner>()
            .RunAsync(data, symbols)
            .ConfigureAwait(false);
    }
}

