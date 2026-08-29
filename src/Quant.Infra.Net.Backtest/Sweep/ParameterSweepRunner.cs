using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Orchestration;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;

namespace Quant.Infra.Net.Backtest.Sweep;

/// <summary>
/// 参数扫描（B4）：对一组策略参数网格并行执行相互独立的完整回测。
/// Parameter sweep (B4): runs a set of strategy-parameter grid points as fully independent backtests, in parallel.
/// </summary>
/// <remarks>
/// 每个网格点使用**全新的 DI 容器 + 全新 <see cref="BacktestBrokerService"/> 实例**（§9 B4：不共享状态），
/// 结果按输入网格顺序返回；并行度可用 <see cref="ParallelOptions.MaxDegreeOfParallelism"/> 控制。
/// Each grid point gets a fresh DI container and a fresh BacktestBrokerService instance (section 9 B4: no shared state);
/// results come back in the input grid order.
/// </remarks>
public sealed class ParameterSweepRunner
{
    private readonly HistoricalDataSet _data;
    private readonly IReadOnlyList<string> _symbols;
    private readonly BacktestOptions _backtestOptions;
    private readonly Action<OrchestrationOptions> _baseOrchestration;

    /// <summary>
    /// 初始化扫描器。
    /// Initializes the sweep runner.
    /// </summary>
    /// <param name="data">历史数据集（全网格点共享只读数据，只读共享无状态风险）/ The historical data set (shared read-only data across points).</param>
    /// <param name="symbols">参与回测的符号 / The symbols under test.</param>
    /// <param name="backtestOptions">回测公共配置（成本/FillTiming/Warmup 等，全网格点相同）/ Shared backtest options (costs / FillTiming / warmup etc.).</param>
    /// <param name="baseOrchestration">对每个网格点都生效的基线编排配置回调（网格参数在其后被覆盖）/ The baseline orchestration configuration applied to every point (grid values override it).</param>
    public ParameterSweepRunner(
        HistoricalDataSet data,
        IReadOnlyList<string> symbols,
        BacktestOptions backtestOptions,
        Action<OrchestrationOptions> baseOrchestration)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _symbols = symbols ?? throw new ArgumentNullException(nameof(symbols));
        _backtestOptions = backtestOptions ?? throw new ArgumentNullException(nameof(backtestOptions));
        _baseOrchestration = baseOrchestration ?? throw new ArgumentNullException(nameof(baseOrchestration));
    }

    /// <summary>
    /// 并行执行参数网格（Parallel.ForEachAsync）；返回按网格顺序排列的独立结果。
    /// Runs the parameter grid in parallel (Parallel.ForEachAsync); returns one independent result per grid point, in grid order.
    /// </summary>
    /// <param name="grid">策略参数网格（每点一组 key/value，如 FastPeriod×SlowPeriod 的笛卡尔积）/ The strategy-parameter grid (each point is a key/value set, e.g. the FastPeriod×SlowPeriod Cartesian product).</param>
    /// <param name="maxDegreeOfParallelism">并行度（≤0 为默认 = 处理器数）/ Parallelism (≤0 = default, i.e. processor count).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>与网格一一对应的回测结果 / One backtest result per grid point, in grid order.</returns>
    public async Task<IReadOnlyList<ParameterSweepPoint>> RunAsync(
        IReadOnlyList<IReadOnlyDictionary<string, string>> grid,
        int maxDegreeOfParallelism = 0,
        CancellationToken ct = default)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }

        var results = new ParameterSweepPoint[grid.Count];
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Environment.ProcessorCount,
            CancellationToken = ct,
        };

        var indexed = new KeyValuePair<int, IReadOnlyDictionary<string, string>>[grid.Count];
        for (var i = 0; i < grid.Count; i++)
        {
            indexed[i] = new KeyValuePair<int, IReadOnlyDictionary<string, string>>(i, grid[i]);
        }

        await Parallel.ForEachAsync(indexed, options, async (item, token) =>
        {
            results[item.Key] = await RunSinglePointAsync(item.Value, token).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return results;
    }

    /// <summary>
    /// 单个网格点：全新 DI 容器 + 全新 broker，独立跑一次完整回测。
    /// One grid point: a fresh DI container plus a fresh broker, running one self-contained backtest.
    /// </summary>
    private async Task<ParameterSweepPoint> RunSinglePointAsync(IReadOnlyDictionary<string, string> parameters, CancellationToken ct)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        // 每个网格点独立的 broker + DI 容器 ⇒ 完全不共享状态（B4 验收要求）。
        // A per-point broker + DI container ⇒ no state is shared at all (B4 acceptance requirement).
        var broker = new BacktestBrokerService(_backtestOptions);
        var services = new ServiceCollection();
        services.TryAddSingleton<IAnalysisService, AnalysisService>();
        services.AddSingleton(broker);
        services.AddSingleton<Quant.Infra.Net.Broker.Interfaces.IBinanceUsdFutureService>(_ => broker);

        services.AddQuantInfraNetOrchestration(o =>
        {
            _baseOrchestration(o);
            foreach (var (key, value) in parameters)
            {
                o.Parameters[key] = value; // 网格参数覆盖基线 / grid values override the baseline
            }
        });

        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<StrategyPipeline>();
        var orchestrationOptions = provider.GetRequiredService<OrchestrationOptions>();
        var runner = new BacktestRunner(pipeline, broker, orchestrationOptions, _backtestOptions);

        var backtest = await runner.RunAsync(_data, _symbols, ct).ConfigureAwait(false);
        return new ParameterSweepPoint(
            new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase),
            backtest);
    }
}

/// <summary>
/// 单个网格点的回测结果（网格坐标 + 完整回测结果）。
/// One grid point's result (the grid coordinates plus the full backtest result).
/// </summary>
/// <param name="Parameters">该点的策略参数坐标 / The strategy-parameter coordinates of the point.</param>
/// <param name="Backtest">该点独立运行的回测结果 / The independently-produced backtest result.</param>
public sealed record ParameterSweepPoint(IReadOnlyDictionary<string, string> Parameters, BacktestResult Backtest);
