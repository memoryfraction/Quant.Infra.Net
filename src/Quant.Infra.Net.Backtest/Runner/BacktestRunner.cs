using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Metrics;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Backtest.Runner;

/// <summary>
/// 回测引擎核心：对数据集中每根时间轴 bar 执行一次完整 StrategyPipeline（事件驱动，非向量化），
/// 逐 bar 注入"截至该时刻"的历史切片（防未来函数，见 §4/§5），并把 broker 权益沉淀为权益曲线。
/// Backtest engine core: one full StrategyPipeline run per timeline bar (event-driven, not vectorized),
/// injecting the as-of slice per bar (look-ahead guard, see §4/§5) and sinking broker equity into the curve.
/// </summary>
/// <remarks>
/// 单 bar 失败语义（§6）：RiskStage 抛出 <see cref="PipelineAbortException"/>（风控拒绝，建议清仓）或
/// Stage 抛出其它异常时，该 bar 的运行终止（本 bar 不计入新交易），引擎记录事件并继续后续 bar。
/// Failure semantics (section 6): on PipelineAbortException (risk rejection, liquidation advised) or any
/// other stage exception, that bar's run ends (no new trades for it); the engine records the event and continues.
/// </remarks>
public sealed class BacktestRunner
{
    private readonly StrategyPipeline _pipeline;
    private readonly BacktestBrokerService _broker;
    private readonly OrchestrationOptions _orchestrationOptions;
    private readonly BacktestOptions _backtestOptions;

    /// <summary>
    /// 初始化回测引擎。
    /// Initializes the backtest engine.
    /// </summary>
    /// <param name="pipeline">策略管道（StrategyPipeline）/ The strategy pipeline.</param>
    /// <param name="broker">回测 broker（回测撮合）/ The backtest broker (simulated matching).</param>
    /// <param name="orchestrationOptions">编排配置（Parameters 决定符号等）/ Orchestration options (Parameters picks symbols etc.).</param>
    /// <param name="backtestOptions">回测配置（FillTiming/成本等）/ Backtest options (FillTiming / costs etc.).</param>
    public BacktestRunner(
        StrategyPipeline pipeline,
        BacktestBrokerService broker,
        OrchestrationOptions orchestrationOptions,
        BacktestOptions backtestOptions)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _orchestrationOptions = orchestrationOptions ?? throw new ArgumentNullException(nameof(orchestrationOptions));
        _backtestOptions = backtestOptions ?? throw new ArgumentNullException(nameof(backtestOptions));
    }

    /// <summary>
    /// 执行一次回测：按时间轴从第 WarmupBars 根起逐 bar 回放，返回权益曲线 / 成交 / 事件（Metrics 由 B4 装配）。
    /// Runs one backtest: replays bar-by-bar from bar WarmupBars and returns the equity curve / trades / events
    /// (Metrics are assembled in B4).
    /// </summary>
    /// <param name="data">历史数据集 / The historical data set.</param>
    /// <param name="symbols">参与回测的符号（与 Parameters 中的符号一致）/ The symbols under test (matching Parameters).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>回测结果（B4 之前 Metrics 为默认值）/ The backtest result (default Metrics before B4).</returns>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when an argument is null.</exception>
    public async Task<BacktestResult> RunAsync(HistoricalDataSet data, IReadOnlyList<string> symbols, CancellationToken ct = default)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (symbols == null)
        {
            throw new ArgumentNullException(nameof(symbols));
        }

        var timeline = data.Timeline;
        var warmup = Math.Clamp(_backtestOptions.WarmupBars, 0, timeline.Count);
        var deferFills = _backtestOptions.FillTiming == FillTiming.NextBarOpen;
        var equityCurve = new Dictionary<DateTime, decimal>();
        var runEvents = new List<PipelineEvent>();
        long runId = 0L;

        _broker.DeferFills = deferFills;
        try
        {
            for (var i = warmup; i < timeline.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var t = timeline[i];

                if (deferFills && i > warmup)
                {
                    // NextBarOpen（§7.1/§12.2）：上一根 bar（信号时刻 anchor）挂起的订单，
                    // 以"s 在 anchor 之后第一根 bar 的开盘价"成交；本 bar 的开盘即其下一根。
                    // NextBarOpen: orders queued at the previous run instant (anchor) fill at
                    // "the first open strictly after anchor" for each symbol — i.e. this bar's open.
                    var anchor = timeline[i - 1];
                    var opens = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    foreach (var s in symbols)
                    {
                        var open = data.OpenAtNextAfter(s, anchor);
                        if (open is not null)
                        {
                            opens[s] = open.Value;
                        }
                    }

                    if (opens.Count > 0)
                    {
                        _broker.SetMarkPrices(opens);
                        _broker.SimulatedNowUtc = t; // 成交时点=本 bar（填充 bar）/ fill instant = this bar
                        _broker.FlushPendingOrders();
                    }

                    // 随后用收盘价标记估值（信号基于本 bar 收盘）。/ Then mark with closes for valuation (signals use this bar's close).
                    var valCloses = ClosesAt(data, symbols, t);
                    if (valCloses.Count > 0)
                    {
                        _broker.SetMarkPrices(valCloses);
                    }
                }
                else
                {
                    // SameBarClose（默认）：标记价=收盘，管道即时成交（§7.1）。
                    // SameBarClose (default): marks = close; the pipeline fills immediately.
                    var closes = ClosesAt(data, symbols, t);
                    if (closes.Count > 0)
                    {
                        _broker.SetMarkPrices(closes);
                    }

                    _broker.SimulatedNowUtc = t;
                }

                // 防未来函数（§4）：只注入"截至 t"的历史切片；DataIngest/SignalDataLoader 优先命中缓存，零拉取。
                // Look-ahead guard (section 4): inject only the as-of slice; ingest/loaders hit the cache, zero fetch.
                var merged = new HashSet<Ohlcv>();
                foreach (var s in symbols)
                {
                    merged.UnionWith(data.SliceUpTo(s, t).OhlcvSet);
                }

                var context = new PipelineContext(runId++, _orchestrationOptions.Parameters);
                if (merged.Count > 0)
                {
                    context.Set(merged);
                }

                try
                {
                    await _pipeline.RunAsync(context, ct).ConfigureAwait(false);
                }
                catch (PipelineAbortException)
                {
                    // 风控拒绝/正常终止：本 bar 作废（曲线保持当前 broker 权益），整体运行不中断（§6）。
                    // Risk rejection / normal abort: this bar is void (curve keeps current broker equity); the run continues.
                }
                catch (Exception ex)
                {
                    // Stage 崩溃：管道已记录事件；单 bar 失效但整体运行不中断。
                    // Stage crash: the pipeline already recorded the event; this bar is void, the run continues.
                    context.AddError(ex);
                }

                runEvents.AddRange(context.Events);

                var snapshot = context.Get<PortfolioSnapshot>();
                var equity = snapshot is not null ? snapshot.AccountEquityUsd : _broker.CurrentEquityUsd;
                equityCurve[t] = equity;
            }
        }
        finally
        {
            // 最后一根 bar 挂起的订单没有"下一根开盘"可成交：不成交、不回填（§7.1 语义）。
            // Orders pending after the last bar have no next-bar open to fill at: they stay unfilled (section 7.1 semantics).
            _broker.DeferFills = false;
        }

        var allTrades = _broker.Trades;
        return new BacktestResult
        {
            EquityCurve = equityCurve,
            Trades = allTrades,
            RunEvents = runEvents,
            // B4：StrategyPerformanceAnalyzer（曲线指标）+ TradeStatistics（交易级）装配，不重新实现同名指标。
            // B4: assembled from StrategyPerformanceAnalyzer (curve metrics) + TradeStatistics (trade-level).
            Metrics = BacktestMetricsFactory.Assemble(equityCurve, allTrades),
        };
    }

    private static Dictionary<string, double> ClosesAt(HistoricalDataSet data, IReadOnlyList<string> symbols, DateTime t)
    {
        var closes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in symbols)
        {
            var close = data.CloseAt(s, t);
            if (close is not null)
            {
                closes[s] = close.Value;
            }
        }

        return closes;
    }
}
