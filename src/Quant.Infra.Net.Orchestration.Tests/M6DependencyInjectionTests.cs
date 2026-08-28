using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Risk;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.Orchestration.State;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// M6：依赖注入契约（§5.8）+ 端到端 Paper 单轮验证 + PipelineRunner 行为。
/// M6: dependency-injection contract (§5.8) + end-to-end Paper single-cycle verification + PipelineRunner behavior.
/// </summary>
/// <remarks>
/// 测试不触及网络：行情数据经由内置 DeterministicYahoo 假货实现提供。
/// No network access in tests: market data is supplied by the hand-written DeterministicYahoo fake.
/// </remarks>
[TestClass]
public sealed class M6DependencyInjectionTests
{
    /// <summary>
    /// 构造基础服务集合（Paper 环境 + C# 分析服务 + 可选策略参数覆盖）。
    /// Builds the base service collection (Paper environment + C# analysis service + optional strategy parameter overrides).
    /// </summary>
    private static (ServiceCollection Services, Dictionary<string, double[]> Series) BaseServices(
        Action<OrchestrationOptions>? configure = null)
    {
        var s = new ServiceCollection();
        var series = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        s.AddOptions();
        s.AddSingleton<IAnalysisService, AnalysisService>();
        s.AddSingleton<ITraditionalFinanceSourceDataService>(_ => new DeterministicYahoo(series));
        s.Configure<OrchestrationOptions>(o =>
        {
            o.Environment = ExchangeEnvironment.Paper; // 显式 Paper（枚举零值是 Testnet）/ explicit Paper (enum zero value is Testnet)
            configure?.Invoke(o);
        });
        return (s, series);
    }

    private static Dictionary<string, double[]> PairSeries()
    {
        // 复用 M2 已验证的确定性配对数据（高相关 + 平稳价差，末点显著偏正 → A=Short / B=Long）。
        // Reuses M2-validated deterministic pair data (high correlation + stationary spread; last point strongly positive → A=Short / B=Long).
        const int n = 120;
        var a = TestSeries.Wave(n);
        var spread = TestSeries.Ar1(n, 0.5, seed: 42);
        spread[n - 1] = 3.0;
        var b = a.Zip(spread, (x, si) => 0.8 * x + 10.0 + si).ToArray();
        return new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["AAA"] = a,
            ["BBB"] = b
        };
    }

    [TestMethod]
    public void Paper_Wires_All_Core_Singletons_In_Default_Order()
    {
        var (services, _) = BaseServices();
        services.AddQuantInfraNetOrchestration();
        using var sp = services.BuildServiceProvider();

        Assert.IsInstanceOfType(sp.GetRequiredService<IBinanceUsdFutureService>(), typeof(PaperBinanceUsdFutureService));
        Assert.IsInstanceOfType(sp.GetRequiredService<IExecutionModel>(), typeof(RebalanceExecutionModel));
        Assert.IsInstanceOfType(sp.GetRequiredService<ISignalGenerator>(), typeof(PairTradingZScoreSignalGenerator));
        Assert.IsNotNull(sp.GetRequiredService<IRiskManager>());
        Assert.IsNotNull(sp.GetRequiredService<IPortfolioStateStore>());
        Assert.IsNotNull(sp.GetRequiredService<INotificationHub>());
        Assert.IsNotNull(sp.GetRequiredService<PipelineRunner>());

        var pipeline = sp.GetRequiredService<StrategyPipeline>();
        var names = pipeline.Stages.Select(st => st.Name).ToList();
        var expectedOrder = new[] { "DataIngest", "Analysis", "Signal", "TargetPosition", "Risk", "Execution", "PortfolioState", "Notification" };
        Assert.IsTrue(names.SequenceEqual(expectedOrder), $"stage order was: {string.Join(",", names)}");
        // 通知路由必须位于管道末端 / the notification hub must wire the last stage
        Assert.IsTrue(sp.GetServices<IHostedService>().Any(h => ReferenceEquals(h, sp.GetRequiredService<PipelineRunner>())));
    }

    [TestMethod]
    [DataRow("MaCross")]
    [DataRow("MeanReversion")]
    [DataRow("PairTradingZScore")]
    public void KnownStrategies_Resolve_Their_Generator(string strategy)
    {
        var (services, _) = BaseServices(configure: o => o.Parameters["Strategy"] = strategy);
        services.AddQuantInfraNetOrchestration();
        using var sp = services.BuildServiceProvider();

        var expectedType = strategy switch
        {
            "PairTradingZScore" => typeof(PairTradingZScoreSignalGenerator),
            "MaCross" => typeof(MaCrossSignalGenerator),
            "MeanReversion" => typeof(MeanReversionSignalGenerator),
            _ => throw new InvalidOperationException(strategy)
        };
        Assert.IsInstanceOfType(sp.GetRequiredService<ISignalGenerator>(), expectedType);
    }

    [TestMethod]
    public void UnknownStrategy_Fails_Fast_With_ArgumentException()
    {
        var (services, _) = BaseServices(configure: o => o.Parameters["Strategy"] = "BogusStrategy");
        services.AddQuantInfraNetOrchestration();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.ThrowsException<ArgumentException>(() => sp.GetRequiredService<ISignalGenerator>());
        StringAssert.Contains(ex.Message, "Unknown Strategy");
    }

    [TestMethod]
    public void NonPaper_Environment_Without_Caller_Broker_Throws_SupportedException()
    {
        var (services, _) = BaseServices(configure: o => o.Environment = ExchangeEnvironment.Live);
        services.AddQuantInfraNetOrchestration();
        using var sp = services.BuildServiceProvider();

        Assert.ThrowsException<NotSupportedException>(() => sp.GetRequiredService<IBinanceUsdFutureService>());
    }

    [TestMethod]
    public void CallerRegistered_Broker_Wins_Over_Paper_Default()
    {
        var caller = new ServiceCollection();
        caller.AddOptions();
        caller.AddSingleton<IAnalysisService, AnalysisService>();
        caller.AddSingleton<ITraditionalFinanceSourceDataService>(_ => new DeterministicYahoo(new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)));
        caller.Configure<OrchestrationOptions>(o => o.Environment = ExchangeEnvironment.Paper);
        var callerBroker = new PaperBinanceUsdFutureService(new OrchestrationOptions());
        caller.AddSingleton<IBinanceUsdFutureService>(callerBroker); // 调用方先行注册 / caller registers first
        caller.AddQuantInfraNetOrchestration();
        using var sp = caller.BuildServiceProvider();

        Assert.AreSame(callerBroker, sp.GetRequiredService<IBinanceUsdFutureService>());
    }

    [TestMethod]
    public void CustomStages_Completely_Replace_Defaults()
    {
        var (services, _) = BaseServices();
        services.AddQuantInfraNetOrchestration(customStages: new IPipelineStage[] { new NoopStage() });
        using var sp = services.BuildServiceProvider();

        var pipeline = sp.GetRequiredService<StrategyPipeline>();
        Assert.AreEqual(1, pipeline.Stages.Count);
        Assert.AreEqual("Noop", pipeline.Stages.Single().Name);
    }

    [TestMethod]
    public async Task Paper_EndToEnd_SingleCycle_Produces_Signals_Execution_Snapshot()
    {
        var (services, series) = BaseServices();
        series.Add("AAA", PairSeries()["AAA"].ToArray());
        series.Add("BBB", PairSeries()["BBB"].ToArray());
        services.Configure<OrchestrationOptions>(o =>
        {
            o.Parameters["Strategy"] = "PairTradingZScore";
            o.Parameters["SymbolA"] = "AAA";
            o.Parameters["SymbolB"] = "BBB";
            o.Parameters["MinCorrelation"] = "0.8";
        });
        services.AddQuantInfraNetOrchestration();
        using var sp = services.BuildServiceProvider();

        // 宿主构建期触达策略解析（fail-fast 校验点）/ host-build time strategy resolution (fail-fast checkpoint)
        sp.GetRequiredService<ISignalGenerator>();
        var pipeline = sp.GetRequiredService<StrategyPipeline>();
        var options = sp.GetRequiredService<IOptions<OrchestrationOptions>>().Value;

        var ctx = new PipelineContext(1, options.Parameters);
        await pipeline.RunAsync(ctx, CancellationToken.None);

        // —— 信号：M2 已验证数据，末点价差显著为正（z≥+1.5）→ AAA=Short / BBB=Long ——
        var signals = ctx.Get<IReadOnlyList<Signal>>();
        Assert.IsNotNull(signals);
        Assert.AreEqual(2, signals!.Count);
        Assert.AreEqual(SignalDirection.Short, signals.First(s => s.Symbol == "AAA").Direction);
        Assert.AreEqual(SignalDirection.Long, signals.First(s => s.Symbol == "BBB").Direction);

        // —— 目标仓位 + 执行报告各两条 —— / two target positions and two execution reports
        var targets = ctx.Get<IReadOnlyList<TargetPosition>>();
        Assert.IsNotNull(targets);
        Assert.AreEqual(2, targets!.Count);
        // —— 审计溯源：每条 TargetPosition 都能回溯到产生它的 Signal —— / audit trail: every target traces back to its originating signal
        Assert.IsTrue(targets.All(t => t.OriginSignal != null), "expected every target to carry its originating signal");
        Assert.IsTrue(targets.All(t => t.OriginSignal!.Symbol == t.Symbol));
        var reports = ctx.Get<IReadOnlyList<ExecutionReport>>();
        Assert.IsNotNull(reports);
        Assert.AreEqual(2, reports!.Count);
        Assert.IsTrue(reports.All(r => r.Success), $"expected all executions to succeed: {string.Join("; ", reports.Select(r => r.ErrorMessage ?? "ok"))}");

        // —— Paper broker 真实落位 + 快照存储 —— / Paper broker holds positions and the snapshot is stored
        var broker = sp.GetRequiredService<IBinanceUsdFutureService>();
        Assert.IsTrue(broker is PaperBinanceUsdFutureService, $"paper broker expected, got {broker?.GetType().Name}");
        var paper = (PaperBinanceUsdFutureService)broker!;
        var positions = await paper.GetHoldingPositionAsync();
        var symbols = positions.Select(p => p.Symbol).Where(s => s != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.IsTrue(symbols.Contains("AAA"), $"positions were: {string.Join(",", symbols)}");
        Assert.IsTrue(symbols.Contains("BBB"), $"positions were: {string.Join(",", symbols)}");

        var store = sp.GetRequiredService<IPortfolioStateStore>();
        var snapshot = await store.GetLatestAsync(CancellationToken.None);
        Assert.IsNotNull(snapshot);
        Assert.IsTrue(snapshot!.AccountEquityUsd > 0m);

        // —— 事件流完整（每阶段留痕）/ the event trail covers every stage
        var stages = ctx.Events.Select(e => e.Stage).ToHashSet();
        foreach (var expected in new[] { "DataIngest", "Analysis", "PairTradingZScore", "TargetPosition", "Risk", "Execution", "PortfolioState", "Notification" })
        {
            Assert.IsTrue(stages.Contains(expected), $"expected an event from stage '{expected}'");
        }
        Assert.AreEqual(0, ctx.Errors.Count, $"errors: {string.Join("; ", ctx.Errors.Select(e => e.Message))}");
    }

    /// <summary>
    /// M6 端到端：MaCross 策略单周期（陡峭上升序列 → close 显著高于 SMA(10) → Long）。
    /// M6 end-to-end: MaCross strategy single cycle (steep uptrend → close well above SMA(10) → Long).
    /// </summary>
    [TestMethod]
    public async Task Paper_EndToEnd_SingleCycle_MaCross_ProducesLongSignal_Execution_Snapshot()
    {
        var (services, series) = BaseServices();
        series.Add("AAA", TestSeries.Wave(40, drift: 0.6));
        services.Configure<OrchestrationOptions>(o =>
        {
            o.Parameters["Strategy"] = "MaCross";
            o.Parameters["Symbol"] = "AAA";
            o.Parameters["FastPeriod"] = "1";
            o.Parameters["SlowPeriod"] = "10";
            o.Parameters["AllowShort"] = "false";
        });
        services.AddQuantInfraNetOrchestration();
        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<ISignalGenerator>();
        var pipeline = sp.GetRequiredService<StrategyPipeline>();
        var options = sp.GetRequiredService<IOptions<OrchestrationOptions>>().Value;

        var ctx = new PipelineContext(1, options.Parameters);
        await pipeline.RunAsync(ctx, CancellationToken.None);

        var signals = ctx.Get<IReadOnlyList<Signal>>();
        Assert.IsNotNull(signals);
        Assert.AreEqual(1, signals!.Count);
        Assert.AreEqual(SignalDirection.Long, signals[0].Direction, $"reason was: {signals[0].Reason}");

        var targets = ctx.Get<IReadOnlyList<TargetPosition>>();
        Assert.IsNotNull(targets);
        Assert.AreEqual(1, targets!.Count);
        Assert.AreSame(signals[0], targets[0].OriginSignal);

        var reports = ctx.Get<IReadOnlyList<ExecutionReport>>();
        Assert.IsNotNull(reports);
        Assert.IsTrue(reports!.All(r => r.Success), $"expected all executions to succeed: {string.Join("; ", reports.Select(r => r.ErrorMessage ?? "ok"))}");

        var broker = (PaperBinanceUsdFutureService)sp.GetRequiredService<IBinanceUsdFutureService>();
        Assert.IsTrue(await broker.HasUsdFuturePositionAsync("AAA"));

        var store = sp.GetRequiredService<IPortfolioStateStore>();
        var snapshot = await store.GetLatestAsync(CancellationToken.None);
        Assert.IsNotNull(snapshot);
        Assert.IsTrue(snapshot!.AccountEquityUsd > 0m);
        Assert.AreEqual(0, ctx.Errors.Count, $"errors: {string.Join("; ", ctx.Errors.Select(e => e.Message))}");
    }

    /// <summary>
    /// M6 端到端：MeanReversion 策略单周期（末点大幅偏离窗口均值 → z ≤ −EntryZ → Long）。
    /// M6 end-to-end: MeanReversion strategy single cycle (last point far below the window mean → z ≤ −EntryZ → Long).
    /// </summary>
    [TestMethod]
    public async Task Paper_EndToEnd_SingleCycle_MeanReversion_ProducesLongSignal_Execution_Snapshot()
    {
        var closes = TestSeries.Wave(30);
        closes[^1] -= 25.0; // 末点显著低于近期均值 → 超跌 / last point drops sharply below the recent mean → oversold
        var (services, series) = BaseServices();
        series.Add("AAA", closes);
        services.Configure<OrchestrationOptions>(o =>
        {
            o.Parameters["Strategy"] = "MeanReversion";
            o.Parameters["Symbol"] = "AAA";
            o.Parameters["LookbackBars"] = "20";
            o.Parameters["EntryZ"] = "1.5";
            o.Parameters["ExitZ"] = "0.5";
            o.Parameters["AllowShort"] = "true";
        });
        services.AddQuantInfraNetOrchestration();
        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<ISignalGenerator>();
        var pipeline = sp.GetRequiredService<StrategyPipeline>();
        var options = sp.GetRequiredService<IOptions<OrchestrationOptions>>().Value;

        var ctx = new PipelineContext(1, options.Parameters);
        await pipeline.RunAsync(ctx, CancellationToken.None);

        var signals = ctx.Get<IReadOnlyList<Signal>>();
        Assert.IsNotNull(signals);
        Assert.AreEqual(1, signals!.Count);
        Assert.AreEqual(SignalDirection.Long, signals[0].Direction, $"reason was: {signals[0].Reason}");

        var targets = ctx.Get<IReadOnlyList<TargetPosition>>();
        Assert.IsNotNull(targets);
        Assert.AreEqual(1, targets!.Count);
        Assert.AreSame(signals[0], targets[0].OriginSignal);

        var reports = ctx.Get<IReadOnlyList<ExecutionReport>>();
        Assert.IsNotNull(reports);
        Assert.IsTrue(reports!.All(r => r.Success), $"expected all executions to succeed: {string.Join("; ", reports.Select(r => r.ErrorMessage ?? "ok"))}");

        var broker = (PaperBinanceUsdFutureService)sp.GetRequiredService<IBinanceUsdFutureService>();
        Assert.IsTrue(await broker.HasUsdFuturePositionAsync("AAA"));

        var store = sp.GetRequiredService<IPortfolioStateStore>();
        var snapshot = await store.GetLatestAsync(CancellationToken.None);
        Assert.IsNotNull(snapshot);
        Assert.IsTrue(snapshot!.AccountEquityUsd > 0m);
        Assert.AreEqual(0, ctx.Errors.Count, $"errors: {string.Join("; ", ctx.Errors.Select(e => e.Message))}");
    }

    [TestMethod]
    public async Task PipelineRunner_RunOnceAsync_Fires_RunCompleted_And_Counts_Runs()
    {
        var (services, _) = BaseServices();
        services.AddQuantInfraNetOrchestration();
        using var sp = services.BuildServiceProvider();

        var runner = sp.GetRequiredService<PipelineRunner>();
        var invocations = new List<PipelineContext>();
        runner.RunCompleted += c => invocations.Add(c);

        var first = await runner.RunOnceAsync(CancellationToken.None);
        var second = await runner.RunOnceAsync(CancellationToken.None);

        Assert.AreEqual(2, invocations.Count);
        Assert.AreSame(first, invocations[0]);
        Assert.AreSame(second, invocations[1]);
        Assert.AreEqual(2, runner.CompletedRuns);
        Assert.AreEqual(1, first.RunId);
        Assert.AreEqual(2, second.RunId);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(0, first.Errors.Count);
        Assert.AreEqual(0, second.Errors.Count);
    }
}

/// <summary>
/// 测试用空操作阶段 / no-op stage for tests.
/// </summary>
internal sealed class NoopStage : Orchestration.Abstractions.IPipelineStage
{
    public string Name => "Noop";

    public Task ExecuteAsync(Orchestration.Models.IPipelineContext context, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// 确定性离线行情假货：按预置收盘价表返回 Ohlcvs（无网络、无状态）。
/// Deterministic offline market-data fake: returns Ohlcvs from pre-seeded close series (no network, stateless).
/// </summary>
internal sealed class DeterministicYahoo : ITraditionalFinanceSourceDataService
{
    private readonly Dictionary<string, double[]> _series;

    public DeterministicYahoo(Dictionary<string, double[]> series)
    {
        _series = new Dictionary<string, double[]>(series, StringComparer.OrdinalIgnoreCase);
    }

    public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, ResolutionLevel Period = ResolutionLevel.Daily)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentNullException(nameof(symbol));
        if (string.IsNullOrWhiteSpace(fullPathFileName)) throw new ArgumentNullException(nameof(fullPathFileName));
        return Task.FromResult(OhlcvsFor(symbol, startDt, 100));
    }

    public Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel Period = ResolutionLevel.Daily, DataSource dataSource = DataSource.YahooFinance)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("symbol is required", nameof(symbol));
        }

        return Task.FromResult(OhlcvsFor(symbol, startDt, Math.Max(50, _series.ContainsKey(symbol) ? _series[symbol].Length : 100)));
    }

    public Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename)
        => Task.FromResult(new List<Ohlcv>());

    public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName)
        => Task.CompletedTask;

    public Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500)
        => Task.FromResult(Enumerable.Empty<string>());

    private Ohlcvs OhlcvsFor(string symbol, DateTime startDt, int bars)
    {
        if (!_series.TryGetValue(symbol, out var closes))
        {
            closes = Enumerable.Repeat(100.0, bars).ToArray();
        }

        var n = Math.Min(bars, closes.Length);
        var set = new HashSet<Ohlcv>();
        for (var i = 0; i < n; i++)
        {
            var dt = startDt + TimeSpan.FromDays(i);
            var price = (decimal)Math.Round(closes[i], 4);
            set.Add(new Ohlcv
            {
                Symbol = symbol,
                OpenDateTime = dt,
                CloseDateTime = dt,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = 1m
            });
        }

        return new Ohlcvs { Symbol = symbol, OhlcvSet = set };
    }
}
