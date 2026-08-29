using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Runtime.Tests;

/// <summary>
/// R4 验收：同一组参数 + 同一段历史，同一根 bar——Backtest 与 Paper 两条真实执行路径的
/// Signal / TargetPosition / RiskAssessment 逐字段精确一致（Signal.GeneratedUtc 除外，墙钟本就应不同）；
/// 并"注入"一个故意带状态 bug 的假策略证明：本回归的逐字段比较机制确实能照出
/// "Backtest 每 bar 调用一次 vs Paper 只调用一次"这类分歧（测试自证有效）。
/// R4 acceptance: with identical parameters and history, on the same bar, the two real execution paths
/// (Backtest vs Paper) must produce field-identical Signal/TargetPosition/RiskAssessment (Signal.GeneratedUtc
/// excluded — wall-clock should legitimately differ); plus an injected intentionally stateful-buggy strategy
/// proving this comparer actually catches "Backtest called per bar vs Paper called once" drift.
/// </summary>
[TestClass]
public sealed class ParityRegressionTests
{
    private const string Symbol = "AAA";

    private static readonly DateTime SeriesStart = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 核心一致性：backtest（逐 bar 回放，镜像 BacktestRunner 的 SameBarClose 单 bar 流程）与 paper
    /// （真实 8 段管道 + 固定自定义数据源）对同一根 bar 输出逐字段相等；且该 bar 真实产出了信号/目标位。
    /// Core parity: Backtest (per-bar replay mirroring BacktestRunner's SameBarClose flow) vs Paper (real 8-stage
    /// pipeline + fixed custom data source) yield field-identical outputs on the same bar — and the bar genuinely
    /// produced a signal/target (no vacuous pass).
    /// </summary>
    [TestMethod]
    public async Task Parity_SameBar_Backtest_And_Paper_Fields_Are_Identical()
    {
        const int BarCount = 210;          // > SlowPeriod(200)，保证 MaCross 有足够历史
        const int ParityBar = 209;        // 一致性对照 bar（含它共 210 根 K 线）
        var data = new HistoricalDataSet(BuildBars(BarCount));
        var backtestSource = new FixedSeriesSource(data);

        var backtest = await RunOneBar(
            mode: RunMode.Backtest,
            data: data,
            barIndex: ParityBar,
            parameters: MaCrossParameters(),
            fixedSource: backtestSource,
            runId: 501);

        var paper = await RunOneBar(
            mode: RunMode.Paper,
            data: data,
            barIndex: ParityBar,
            parameters: MaCrossParameters(),
            fixedSource: new FixedSeriesSource(data),
            runId: 502);

        // 该 bar 必须真实产出一个 Long 信号 + 一个目标仓位（排除"两边都空"的假绿）。
        Assert.AreEqual(1, backtest.Signals.Count, "backtest bar must emit exactly one signal");
        Assert.AreEqual(SignalDirection.Long, backtest.Signals[0].Direction);
        Assert.AreEqual(1, backtest.Targets.Count, "backtest bar must emit exactly one target position");
        Assert.IsNotNull(backtest.Risk, "backtest bar must carry a risk assessment");
        Assert.IsTrue(backtest.Risk.Approved, string.Join("; ", backtest.Risk.Reasons));
        Assert.AreEqual(1, paper.Signals.Count, "paper bar must emit exactly one signal");
        Assert.IsFalse(backtest.Aborted);
        Assert.IsFalse(paper.Aborted);

        var diffs = CompareBarOutputs(backtest, paper);
        Assert.AreEqual(0, diffs.Count, "Backtest vs Paper field parity violations: " + string.Join(" | ", diffs));
    }

    /// <summary>
    /// 测试自证：同一个"有状态 bug"策略——Backtest 连跑 3 根 bar（每 bar 调用一次），Paper 跑 1 根（只调用一次）——
    /// 同一逻辑 bar 两侧信号强度必然漂移（3 vs 1），且 CompareBarOutputs 必须照出 Strength 与 Reason 的分歧。
    /// Self-validation: the same stateful-buggy strategy run 3 bars on Backtest (one call per bar) vs 1 bar on Paper
    /// (one call) must drift in strength on the same logical bar (3 vs 1), and CompareBarOutputs must flag the
    /// Strength and Reason drift.
    /// </summary>
    [TestMethod]
    public async Task Parity_Comparer_Catches_Stateful_Drift_Between_MultiCall_Backtest_And_SingleCall_Paper()
    {
        BuggyStatefulSignalGenerator.ResetForTest();
        const int BarCount = 210;
        var data = new HistoricalDataSet(BuildBars(BarCount));
        var parameters = new Dictionary<string, string>
        {
            // 容器启动走合法策略名（目录解析成立）；下面再把"有状态 bug"的假策略直接注入容器（MS.DI 后注册者优先）。
            ["Strategy"] = "MaCross",
            ["Symbol"] = Symbol,
        };
        // 注意：Reset 只能放在测试方法里显式调用（两阶段前各一次）——若放进下方闭包，会在每根 bar 重建容器时清零。
        // Reset is only called explicitly in the test body (once per phase) — putting it in the closure below would
        // zero the counter on every per-bar container rebuild.
        var injectBuggy = (ServiceCollection s) =>
        {
            s.AddSingleton<ISignalGenerator>(new BuggyStatefulSignalGenerator());
        };

        // Backtest：连续 3 根 bar，注入的假策略被调用 3 次（最后一次的信号强度应为 3）。
        // 循环体（bar 207..209）保证至少赋值一次 / the loop (bars 207..209) guarantees at least one assignment.
        BarOutput lastBacktest = null!;
        for (var bar = 207; bar <= 209; bar++)
        {
            lastBacktest = await RunOneBar(
                mode: RunMode.Backtest,
                data: data,
                barIndex: bar,
                parameters: parameters,
                fixedSource: new FixedSeriesSource(data),
                runId: 600 + bar,
                postConfigure: injectBuggy);
        }
        BuggyStatefulSignalGenerator.ResetForTest();

        // Paper：同一逻辑 bar，假策略只被调用 1 次（计数从 0 重新起，强度应为 1）。
        var paper = await RunOneBar(
            mode: RunMode.Paper,
            data: data,
            barIndex: 209,
            parameters: parameters,
            fixedSource: new FixedSeriesSource(data),
            runId: 700,
            postConfigure: injectBuggy);

        Assert.AreEqual(3.0, lastBacktest.Signals[0].Strength, "backtest's 3rd call must carry strength 3");
        Assert.AreEqual(1.0, paper.Signals[0].Strength, "paper's sole call (fresh counter) must carry strength 1 — same bar, different call counts");

        var diffs = CompareBarOutputs(lastBacktest, paper);
        Assert.IsTrue(diffs.Any(d => d.Contains("Strength")), "comparer must flag the Strength drift. got: " + string.Join(" | ", diffs));
        Assert.IsTrue(diffs.Any(d => d.Contains("Reason")), "comparer must flag the Reason drift. got: " + string.Join(" | ", diffs));
    }

    /// <summary>
    /// 全链路驱动：统一入口（Backtest 模式）解析出的 BacktestRunner 对 210 根 bar 完整回放，
    /// 产出 210 点权益曲线且至少一笔成交（第 200 根 bar 起 MaCross 转多，零费）。
    /// End-to-end driver: the BacktestRunner resolved through the unified entry replays all 210 bars,
    /// producing a 210-point equity curve and at least one fill (MaCross goes long from bar 200, zero commission).
    /// </summary>
    [TestMethod]
    public async Task Backtest_FullRunnerViaUnifiedContainer_Produces_EquityCurve_And_Fill()
    {
        var data = new HistoricalDataSet(BuildBars(210));

        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt => rt.RunMode = RunMode.Backtest,
            configureOrchestration: o =>
            {
                foreach (var kv in MaCrossParameters())
                {
                    o.Parameters[kv.Key] = kv.Value;
                }
            });
        using ServiceProvider sp = services.BuildServiceProvider();

        var runner = sp.GetRequiredService<BacktestRunner>();
        var result = await runner.RunAsync(data, new[] { Symbol }, CancellationToken.None);

        Assert.AreEqual(data.Timeline.Count, result.EquityCurve.Count, "every simulated bar must land on the equity curve");
        Assert.IsTrue(result.EquityCurve.Values.All(v => v > 0m));
        Assert.IsTrue(result.Trades.Count >= 1, "a long entry fill is expected from bar 200 onward");
    }

    // —— 实现细节 / implementation detail ————————————————————————————————————————

    private sealed record BarOutput
    {
        public required IReadOnlyList<Signal> Signals { get; init; }

        public required IReadOnlyList<TargetPosition> Targets { get; init; }

        public required RiskAssessment? Risk { get; init; }

        public required bool Aborted { get; init; }
    }

    /// <summary>
    /// 在统一入口构造的容器里跑"同一根 bar"：
    /// Backtest 模式完全镜像 BacktestRunner 的 SameBarClose 单 bar 流程（标记价=该 bar 收盘价、
    /// SetMarkPrices、SimulatedNowUtc、注入截至该 bar 的切片、新 PipelineContext + Parameters、pipeline.RunAsync）；
    /// Paper 模式走真实管道（DataIngest 经固定数据源取同一份切片）。
    /// Runs one bar inside a unified-entry container: Backtest mirrors BacktestRunner's SameBarClose single-bar flow
    /// exactly (marks = that bar's close, SetMarkPrices, SimulatedNowUtc, as-of slice injection, fresh PipelineContext
    /// + Parameters, pipeline.RunAsync); Paper runs the real pipeline (DataIngest fetches the same slice via the fixed
    /// custom data source).
    /// </summary>
    private static Task<BarOutput> RunOneBar(
        RunMode mode,
        HistoricalDataSet data,
        int barIndex,
        IReadOnlyDictionary<string, string> parameters,
        FixedSeriesSource fixedSource,
        long runId,
        Action<ServiceCollection>? postConfigure = null)
    {
        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt =>
            {
                rt.RunMode = mode;
                if (mode == RunMode.Paper)
                {
                    rt.DataSource = DataSourceKind.Custom;
                }
            },
            configureOrchestration: o =>
            {
                foreach (var kv in parameters)
                {
                    o.Parameters[kv.Key] = kv.Value;
                }
            },
            customDataSource: mode == RunMode.Paper ? fixedSource : null);

        // R4 额外用例的注入口：AddQuantInfraNet 之后的直接注册，MS.DI 后注册者优先。
        // Injection hook for the R4 extra case: a direct registration after AddQuantInfraNet (last registration wins).
        postConfigure?.Invoke(services);

        using ServiceProvider sp = services.BuildServiceProvider();
        var pipeline = sp.GetRequiredService<StrategyPipeline>();
        var parametersFromContainer = sp.GetRequiredService<OrchestrationOptions>().Parameters;

        var t = data.Timeline[barIndex];
        if (sp.GetRequiredService<IBinanceUsdFutureService>() is BacktestBrokerService backtestBroker)
        {
            // 与 BacktestRunner RunAsync 同一语义：标记价=该 bar 收盘价，模拟时刻=该 bar（§7.1 SameBarClose）。
            backtestBroker.SetMarkPrices(new Dictionary<string, double> { [Symbol] = data.CloseAt(Symbol, t)!.Value });
            backtestBroker.SimulatedNowUtc = t;
        }

        var merged = new HashSet<Ohlcv>();
        merged.UnionWith(data.SliceUpTo(Symbol, t).OhlcvSet);
        var context = new PipelineContext(runId, parametersFromContainer);
        if (merged.Count > 0)
        {
            context.Set(merged);
        }

        var aborted = false;
        try
        {
            pipeline.RunAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (PipelineAbortException)
        {
            aborted = true; // 风控拒绝：该 bar 作废（BacktestRunner 相同处理），输出槽仍在——两侧对照时一并比较。
        }

        return Task.FromResult(new BarOutput
        {
            Signals = context.Get<IReadOnlyList<Signal>>() ?? Array.Empty<Signal>(),
            Targets = context.Get<IReadOnlyList<TargetPosition>>() ?? Array.Empty<TargetPosition>(),
            Risk = context.Get<RiskAssessment>(),
            Aborted = aborted,
        });
    }

    /// <summary>
    /// 逐字段比较两条路径的同一 bar 输出（Signal.GeneratedUtc 有意不比较——两次运行发生在不同墙钟时刻，本就应不同）。
    /// Compares the two paths' outputs for one bar field by field (Signal.GeneratedUtc deliberately excluded — the two
    /// runs happen at different wall-clock instants and should legitimately differ).
    /// </summary>
    private static List<string> CompareBarOutputs(BarOutput backtest, BarOutput paper)
    {
        var diffs = new List<string>();

        if (backtest.Signals.Count != paper.Signals.Count)
        {
            diffs.Add($"signal count: backtest={backtest.Signals.Count} vs paper={paper.Signals.Count}");
        }
        for (var i = 0; i < Math.Min(backtest.Signals.Count, paper.Signals.Count); i++)
        {
            var (x, y) = (backtest.Signals[i], paper.Signals[i]);
            if (!string.Equals(x.Symbol, y.Symbol, StringComparison.Ordinal))
            {
                diffs.Add($"signal[{i}].Symbol: {x.Symbol} vs {y.Symbol}");
            }
            if (x.Direction != y.Direction)
            {
                diffs.Add($"signal[{i}].Direction: {x.Direction} vs {y.Direction}");
            }
            if (x.Strength != y.Strength)
            {
                diffs.Add($"signal[{i}].Strength: {x.Strength} vs {y.Strength}");
            }
            if (!string.Equals(x.Reason, y.Reason, StringComparison.Ordinal))
            {
                diffs.Add($"signal[{i}].Reason: {x.Reason} vs {y.Reason}");
            }
        }

        if (backtest.Targets.Count != paper.Targets.Count)
        {
            diffs.Add($"target count: backtest={backtest.Targets.Count} vs paper={paper.Targets.Count}");
        }
        for (var i = 0; i < Math.Min(backtest.Targets.Count, paper.Targets.Count); i++)
        {
            var (x, y) = (backtest.Targets[i], paper.Targets[i]);
            if (!string.Equals(x.Symbol, y.Symbol, StringComparison.Ordinal))
            {
                diffs.Add($"target[{i}].Symbol: {x.Symbol} vs {y.Symbol}");
            }
            if (x.TargetWeight != y.TargetWeight)
            {
                diffs.Add($"target[{i}].TargetWeight: {x.TargetWeight} vs {y.TargetWeight}");
            }

            var (ox, oy) = (x.OriginSignal, y.OriginSignal);
            if ((ox is null) != (oy is null))
            {
                diffs.Add($"target[{i}].OriginSignal presence: backtest={(ox is null ? "null" : "set")} vs paper={(oy is null ? "null" : "set")}");
            }
            else if (ox is not null && oy is not null)
            {
                if (!string.Equals(ox.Symbol, oy.Symbol, StringComparison.Ordinal))
                {
                    diffs.Add($"target[{i}].OriginSignal.Symbol: {ox.Symbol} vs {oy.Symbol}");
                }
                if (ox.Direction != oy.Direction)
                {
                    diffs.Add($"target[{i}].OriginSignal.Direction: {ox.Direction} vs {oy.Direction}");
                }
                if (ox.Strength != oy.Strength)
                {
                    diffs.Add($"target[{i}].OriginSignal.Strength: {ox.Strength} vs {oy.Strength}");
                }
                if (!string.Equals(ox.Reason, oy.Reason, StringComparison.Ordinal))
                {
                    diffs.Add($"target[{i}].OriginSignal.Reason: {ox.Reason} vs {oy.Reason}");
                }
            }
        }

        if ((backtest.Risk is null) != (paper.Risk is null))
        {
            diffs.Add($"risk presence: backtest={(backtest.Risk is null ? "null" : "set")} vs paper={(paper.Risk is null ? "null" : "set")}");
        }
        else if (backtest.Risk is not null && paper.Risk is not null)
        {
            if (backtest.Risk.Approved != paper.Risk.Approved)
            {
                diffs.Add($"risk.Approved: backtest={backtest.Risk.Approved} vs paper={paper.Risk.Approved}");
            }
            if (backtest.Risk.Reasons.Count != paper.Risk.Reasons.Count)
            {
                diffs.Add($"risk.Reasons count: {backtest.Risk.Reasons.Count} vs {paper.Risk.Reasons.Count}");
            }
            for (var i = 0; i < Math.Min(backtest.Risk.Reasons.Count, paper.Risk.Reasons.Count); i++)
            {
                if (!string.Equals(backtest.Risk.Reasons[i], paper.Risk.Reasons[i], StringComparison.Ordinal))
                {
                    diffs.Add($"risk.Reasons[{i}]: {backtest.Risk.Reasons[i]} vs {paper.Risk.Reasons[i]}");
                }
            }
        }

        return diffs;
    }

    /// <summary>
    /// 确定性行情：210 根日线，收盘价 100m + 0.5m*i（严格单调递增 → MaCross 在 201 根历史后市价高于慢均线，必出 Long）。
    /// Deterministic fixture: 210 daily bars, close = 100 + 0.5*i (strictly monotonic — MaCross turns Long once 201
    /// bars exist and price is above the slow MA).
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<Ohlcv>> BuildBars(int count)
    {
        var bars = new List<Ohlcv>(count);
        for (var i = 0; i < count; i++)
        {
            var openTime = SeriesStart.AddDays(i);
            var close = 100m + 0.5m * i;
            bars.Add(new Ohlcv
            {
                Symbol = Symbol,
                OpenDateTime = openTime,
                CloseDateTime = openTime.AddDays(1),
                Open = close - 1m,
                High = close + 1m,
                Low = close - 2m,
                Close = close,
                Volume = 1000m,
            });
        }

        return new Dictionary<string, IReadOnlyList<Ohlcv>> { [Symbol] = bars };
    }

    /// <summary>MaCross 参数（最小集合：策略名 + 标的；其余走默认 FastPeriod=1 / SlowPeriod=200 / WeightPerSymbol=0.3）。</summary>
    private static IReadOnlyDictionary<string, string> MaCrossParameters()
    {
        return new Dictionary<string, string>
        {
            ["Strategy"] = "MaCross",
            ["Symbol"] = Symbol,
        };
    }

    /// <summary>
    /// Paper 路径固定数据源：DownloadOhlcvListAsync 恒返回"截至该 bar"的历史切片（其余成员不支持）。
    /// Fixed data source for the Paper path: DownloadOhlcvListAsync always returns the as-of slice (other members
    /// unsupported).
    /// </summary>
    private sealed class FixedSeriesSource : ITraditionalFinanceSourceDataService
    {
        private readonly Ohlcvs _slice;

        public FixedSeriesSource(HistoricalDataSet data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _slice = data.SliceUpTo(Symbol, data.Timeline[^1]);
        }

        public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(
            string symbol,
            DateTime startDt,
            DateTime endDt,
            string fullPathFileName,
            ResolutionLevel Period = ResolutionLevel.Daily)
            => throw new NotSupportedException(nameof(BeginSyncSourceDailyDataAsync));

        public Task<Ohlcvs> DownloadOhlcvListAsync(
            string symbol,
            DateTime startDt,
            DateTime endDt,
            ResolutionLevel Period = ResolutionLevel.Daily,
            DataSource dataSource = DataSource.YahooFinance)
            => Task.FromResult(_slice);

        public Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename)
            => throw new NotSupportedException(nameof(GetOhlcvListAsync));

        public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName)
            => throw new NotSupportedException(nameof(SaveOhlcvListAsync));

        public Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500)
            => throw new NotSupportedException(nameof(GetSp500SymbolsAsync));
    }

    /// <summary>
    /// R4"测试自证"用策略：故意带上状态 bug——每次 <see cref="GenerateSignalsAsync"/> 调用都会推进一个共享可变静态计数，
    /// 信号强度 = 该信号的"第几次被调用"。Backtest 每 bar 调用一次、Paper 只调用一次，两条路径对同一逻辑 bar 的
    /// 信号必然不同——用于证明 CompareBarOutputs 能照出这类分歧。
    /// Self-validation strategy: intentionally carries a stateful bug — every GenerateSignalsAsync call advances a shared
    /// mutable static counter; the signal strength equals "which call produced this signal". Backtest calls it once per
    /// bar, Paper once — the same logical bar must yield different signals, proving the comparer catches such drift.
    /// </summary>
    public sealed class BuggyStatefulSignalGenerator : ISignalGenerator
    {
        private static int _callCount;

        private static string? _lastReason;

        /// <summary>测试隔离：重置调用计数 / Test isolation: reset the call counter (shared static — called between
        /// the Backtest phase and the Paper phase of the self-validation test).</summary>
        public static void ResetForTest()
        {
            _callCount = 0;
            _lastReason = null;
        }

        /// <inheritdoc />
        public string Id => "BuggyStateful";

        /// <inheritdoc />
        public Task<IReadOnlyList<Signal>> GenerateSignalsAsync(IPipelineContext context, CancellationToken ct)
        {
            _callCount++;
            _lastReason = $"stateful drift — call #{_callCount}";
            var signal = new Signal
            {
                Symbol = context.GetParameter("Symbol") ?? "UNKNOWN",
                GeneratedUtc = DateTime.UtcNow,
                Direction = SignalDirection.Long,
                Strength = _callCount,
                Reason = _lastReason,
            };
            return Task.FromResult<IReadOnlyList<Signal>>(new[] { signal });
        }
    }
}
