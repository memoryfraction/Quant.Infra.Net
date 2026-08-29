using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.State;
using Quant.Infra.Net.Orchestration.Stages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// TargetPositionStage 单元测试（信号 → 目标权重映射）。
/// TargetPositionStage unit tests (signal → target weight mapping).
/// </summary>
[TestClass]
public class TargetPositionStageTests
{
    private static TargetPositionStage NewStage(double maxWeightPerSymbol = 0.5d)
        => new(new OrchestrationOptions { MaxWeightPerSymbol = maxWeightPerSymbol });

    private static PipelineContext NewContext(Dictionary<string, string>? parameters = null)
        => new(400, parameters);

    /// <summary>
    /// 配对信号（A=Short, B=Long）→ 相反方向等权目标。
    /// Pair signals (A=Short, B=Long) → opposite-direction equal-weight targets.
    /// </summary>
    [TestMethod]
    public async Task PairSignals_OppositeWeights()
    {
        var ctx = NewContext();
        ctx.Set<IReadOnlyList<Signal>>(new List<Signal>
        {
            new() { Symbol = "AAA", Direction = SignalDirection.Short, Strength = 2.0, Reason = "test" },
            new() { Symbol = "BBB", Direction = SignalDirection.Long, Strength = 2.0, Reason = "test" }
        });

        await NewStage().ExecuteAsync(ctx, CancellationToken.None);

        var targets = ctx.Get<IReadOnlyList<TargetPosition>>()!;
        Assert.AreEqual(2, targets.Count);
        Assert.AreEqual(-0.3, targets.First(t => t.Symbol == "AAA").TargetWeight, 1e-9);
        Assert.AreEqual(0.3, targets.First(t => t.Symbol == "BBB").TargetWeight, 1e-9);
    }

    /// <summary>
    /// Flat 信号 → 目标权重 0。
    /// Flat signals → target weight 0.
    /// </summary>
    [TestMethod]
    public async Task FlatSignal_WeightZero()
    {
        var ctx = NewContext();
        ctx.Set<IReadOnlyList<Signal>>(new List<Signal>
        {
            new() { Symbol = "AAA", Direction = SignalDirection.Flat, Reason = "test" }
        });

        await NewStage().ExecuteAsync(ctx, CancellationToken.None);

        var targets = ctx.Get<IReadOnlyList<TargetPosition>>()!;
        Assert.AreEqual(0d, targets.Single().TargetWeight, 1e-9);
    }

    /// <summary>
    /// WeightPerSymbol 参数生效；超过 MaxWeightPerSymbol 时被截断。
    /// WeightPerSymbol parameter takes effect; capped at MaxWeightPerSymbol.
    /// </summary>
    [TestMethod]
    public async Task WeightParameter_CappedByMax()
    {
        var ctx = NewContext(new Dictionary<string, string> { ["WeightPerSymbol"] = "0.9" });
        ctx.Set<IReadOnlyList<Signal>>(new List<Signal>
        {
            new() { Symbol = "AAA", Direction = SignalDirection.Long, Reason = "test" }
        });

        await NewStage(maxWeightPerSymbol: 0.5d).ExecuteAsync(ctx, CancellationToken.None);

        Assert.AreEqual(0.5, ctx.Get<IReadOnlyList<TargetPosition>>()!.Single().TargetWeight, 1e-9);
    }

    /// <summary>
    /// 无信号 → 空目标列表（保持现有持仓，不平仓）。
    /// No signals → empty target list (hold current positions; do not flatten).
    /// </summary>
    [TestMethod]
    public async Task NoSignals_EmptyTargets()
    {
        var ctx = NewContext();
        await NewStage().ExecuteAsync(ctx, CancellationToken.None);
        Assert.AreEqual(0, ctx.Get<IReadOnlyList<TargetPosition>>()!.Count);
    }

    /// <summary>
    /// null 上下文 → ArgumentNullException。
    /// Null context → ArgumentNullException.
    /// </summary>
    [TestMethod]
    public void NullContext_Throws()
    {
        var stage = NewStage();
        Assert.ThrowsException<ArgumentNullException>(() => stage.ExecuteAsync(null!, CancellationToken.None));
    }
}

/// <summary>
/// ExecutionStage 与 PortfolioStateStage 单元测试（Paper 全链路，零网络）。
/// ExecutionStage and PortfolioStateStage unit tests (full Paper chain, zero network).
/// </summary>
[TestClass]
public class ExecutionAndPortfolioStateStageTests
{
    /// <summary>
    /// ExecutionStage：目标 +0.3 → Paper 建立多头持仓 + 上下文执行报告成功。
    /// ExecutionStage: target +0.3 → Paper opens a long + reports success in context.
    /// </summary>
    [TestMethod]
    public async Task ExecutionStage_OpensPositionAndRecordsReports()
    {
        var options = new OrchestrationOptions();
        var paper = new PaperBinanceUsdFutureService(options);
        paper.SetMarkPrice("AAPL", 100d);
        var broker = new BinanceUsdFutureExecutionBrokerAdapter(paper);
        var model = new RebalanceExecutionModel(broker, options);
        var stage = new ExecutionStage(model, broker);

        var ctx = new PipelineContext(500);
        ctx.Set<IReadOnlyList<TargetPosition>>(new List<TargetPosition>
        {
            new() { Symbol = "AAPL", TargetWeight = 0.3d }
        });

        await stage.ExecuteAsync(ctx, CancellationToken.None);

        Assert.IsTrue(await paper.HasUsdFuturePositionAsync("AAPL"));
        var reports = ctx.Get<IReadOnlyList<ExecutionReport>>()!;
        Assert.AreEqual(1, reports.Count);
        Assert.IsTrue(reports[0].Success);
        Assert.AreEqual("Execution", stage.Name);
    }

    /// <summary>
    /// PortfolioStateStage：上下文行情自动填充 Paper 标记价；快照写入 store 与上下文。
    /// PortfolioStateStage: context data fills Paper marks; snapshot written to store and context.
    /// </summary>
    [TestMethod]
    public async Task PortfolioStateStage_SavesSnapshot()
    {
        var options = new OrchestrationOptions();
        var paper = new PaperBinanceUsdFutureService(options);
        var store = new InMemoryPortfolioStateStore();
        var broker = new BinanceUsdFutureExecutionBrokerAdapter(paper);
        var model = new RebalanceExecutionModel(broker, options);
        var execution = new ExecutionStage(model, broker);
        var state = new PortfolioStateStage(broker, store);

        var ctx = new PipelineContext(600);
        // 上下文行情：AAPL 最新收盘 100（单 Ohlcvs 槽）
        ctx.Set(TestSeries.Build("AAPL", new[] { 90d, 95d, 100d }));
        ctx.Set<IReadOnlyList<Signal>>(new List<Signal>
        {
            new() { Symbol = "AAPL", Direction = SignalDirection.Long, Reason = "test" }
        });

        var targetStage = new TargetPositionStage(options);
        await targetStage.ExecuteAsync(ctx, CancellationToken.None);
        await execution.ExecuteAsync(ctx, CancellationToken.None);
        await state.ExecuteAsync(ctx, CancellationToken.None);

        var snapshot = ctx.Get<PortfolioSnapshot>();
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(10000m, snapshot.AccountEquityUsd);
        var stored = await store.GetLatestAsync(CancellationToken.None);
        Assert.AreSame(snapshot, stored);
        Assert.IsTrue(snapshot.ActualWeights.ContainsKey("AAPL"));
        Assert.AreEqual(0.3, snapshot.ActualWeights["AAPL"], 1e-6);
        Assert.AreEqual(0.3, snapshot.TargetWeights["AAPL"], 1e-9);
        Assert.IsTrue(ctx.Events.Any(e => e.Stage == "PortfolioState"));
    }
}
