using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Notifications;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.Orchestration.Stages;
using Quant.Infra.Net.Runtime.Strategies;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Runtime.Console.Strategies;

/// <summary>
/// R9 范例策略：QQQM 逆向 MA200 定投（用 R7 的 customStages 自定义一个继承自 <see cref="Strategy"/> 的阶段）。
/// 职责：读取 QQQM 收盘价（复用基类的 SignalDataLoader 装载规则）→ 算 SMA200 →
/// 按下式算 targetWeight → 产出一条 Signal 与一条 TargetPosition 写入 context，
/// 交由内置 Risk/Execution/PortfolioState/Notification 四阶段接管（不自造）：
///   ratio = close / SMA200;  deviation = 1 - ratio
///   targetWeight = deviation >= 0 ? BaseWeight + AddIntensity * deviation   // 跌破均线：越跌买越多
///                  :          BaseWeight + TrimIntensity * deviation        // 突破均线：越涨越少
///   targetWeight = clamp(targetWeight, MinWeight, MaxWeight)
/// 参数（Orchestration.Parameters，均带默认值）：Symbol=QQQM, MaPeriod=200, BaseWeight=0.5,
/// AddIntensity=1.5, TrimIntensity=1.0, MaxWeight=1.0, MinWeight=0.0。
/// R9 example: a QQQM reverse-MA200 DCA strategy wired via the R7 customStages parameter as a stage that
/// inherits the Strategy base. Reads closes through the base (SignalDataLoader), computes SMA200 + targetWeight,
/// emits one Signal + one TargetPosition into the context; the built-in Risk/Execution/PortfolioState/Notification stages follow.
/// </summary>
public static class QqqmReverseDcaStrategy
{
    /// <summary>默认参数表（键顺序即文档顺序）/ Default parameter table (key order = documentation order).</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultParameters =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Symbol"] = "QQQM",
            ["MaPeriod"] = "200",
            ["BaseWeight"] = "0.5",
            ["AddIntensity"] = "1.5",
            ["TrimIntensity"] = "1.0",
            ["MaxWeight"] = "1.0",
            ["MinWeight"] = "0.0",
        };

    /// <summary>
    /// 按官方公式计算 targetWeight（纯函数，供 Stage 与单元测试共用）。
    /// Computes targetWeight per the official formula (pure function shared by the stage and the unit tests).
    /// </summary>
    /// <param name="close">最新收盘价 / Latest close.</param>
    /// <param name="sma">均线值（SMA200）/ Moving-average value (SMA200).</param>
    /// <param name="baseWeight">基础权重 / Base weight.</param>
    /// <param name="addIntensity">跌破均线的加仓强度 / Add intensity (below the MA).</param>
    /// <param name="trimIntensity">突破均线的减仓强度 / Trim intensity (above the MA).</param>
    /// <param name="minWeight">权重下限 / Weight lower bound.</param>
    /// <param name="maxWeight">权重上限 / Weight upper bound.</param>
    /// <returns>clamp 后的目标权重 / The clamped target weight.</returns>
    public static double ComputeTargetWeight(
        double close, double sma, double baseWeight, double addIntensity,
        double trimIntensity, double minWeight, double maxWeight)
    {
        if (sma <= 0.0)
        {
            throw new ArgumentException("SMA must be positive.", nameof(sma));
        }

        var ratio = close / sma;
        var deviation = 1.0 - ratio;
        var raw = deviation >= 0
            ? baseWeight + addIntensity * deviation
            : baseWeight + trimIntensity * deviation;
        return Math.Clamp(raw, Math.Min(minWeight, maxWeight), Math.Max(minWeight, maxWeight));
    }

    /// <summary>
    /// 组装自定义阶段序列：本策略阶段（继承自 <see cref="Strategy"/>）+ 内置 Risk/Execution/PortfolioState/Notification 四阶段
    /// （固定顺序，Notification 最后）。
    /// </summary>
    /// <remarks>
    /// 接收的是一个**已经完成 <c>AddQuantInfraNet</c> 注册并 Build 过**的 <see cref="IServiceProvider"/>——
    /// 不能反过来接收尚未注册的 <see cref="IServiceCollection"/>：customStages 这个参数本身是"调用方先造好
    /// Stage 实例、再传给 AddQuantInfraNet"的形状，而 Stage 需要的 IRiskManager/IExecutionModel 等服务
    /// 恰恰是 AddQuantInfraNet 才会注册的——在同一个尚未 Build 的 IServiceCollection 上二次 BuildServiceProvider
    /// 只会拿到一个空容器，GetRequiredService 必然抛异常。正确用法见 <see cref="RunExampleAsync"/>：先用默认
    /// 管道调用 AddQuantInfraNet、Build 一次容器，再用这个容器现造自定义管道并手工构造 BacktestRunner，
    /// 不依赖 AddQuantInfraNet 的 customStages 参数。
    /// Takes an <see cref="IServiceProvider"/> that has **already** been built after AddQuantInfraNet — not an
    /// unregistered IServiceCollection: customStages is shaped as "caller builds the stages first, then passes
    /// them into AddQuantInfraNet", yet the stages need IRiskManager/IExecutionModel/etc., which is exactly what
    /// AddQuantInfraNet itself registers. Building a second container from the same not-yet-registered
    /// IServiceCollection only yields an empty container — GetRequiredService would always throw. See
    /// <see cref="RunExampleAsync"/> for the correct sequencing: call AddQuantInfraNet with the default pipeline,
    /// build the container once, then assemble the custom pipeline from that container and construct
    /// BacktestRunner by hand — bypassing AddQuantInfraNet's customStages parameter entirely.
    /// </remarks>
    /// <param name="sp">已 Build 的服务容器（含 AddQuantInfraNet 注册的全部服务）/ An already-built provider (with everything AddQuantInfraNet registered).</param>
    /// <returns>五个阶段的序列 / The five-stage sequence.</returns>
    /// <exception cref="ArgumentNullException">sp 为 null / Thrown when sp is null.</exception>
    public static IEnumerable<IPipelineStage> BuildPipeline(IServiceProvider sp)
    {
        if (sp == null)
        {
            throw new ArgumentNullException(nameof(sp));
        }

        return new IPipelineStage[]
        {
            new QqqmReverseDcaStage(
                sp.GetService<ITraditionalFinanceSourceDataService>(),
                sp.GetService<IBinanceUsdFutureService>()),
            new RiskStage(
                sp.GetRequiredService<IRiskManager>(),
                sp.GetRequiredService<INotificationHub>(),
                sp.GetRequiredService<IPortfolioStateStore>()),
            new ExecutionStage(
                sp.GetRequiredService<IExecutionModel>(),
                sp.GetRequiredService<IBinanceUsdFutureService>()),
            new PortfolioStateStage(
                sp.GetRequiredService<IBinanceUsdFutureService>(),
                sp.GetRequiredService<IPortfolioStateStore>()),
            new NotificationStage(sp.GetRequiredService<INotificationHub>()),
        };
    }

    /// <summary>
    /// 最小可运行示例（独立可选入口）：Stooq 数据源 + Backtest 模式，输出 CAGR / Sharpe / MaxDrawdown。
    /// 不改动 appsettings.json 的默认启动路径（默认启动不发真实网络请求）。
    /// Minimal runnable example (standalone optional entry): Stooq + Backtest, prints CAGR / Sharpe / MaxDrawdown.
    /// Does not touch the default appsettings.json startup path.
    /// </summary>
    /// <remarks>
    /// 先用默认（八阶段）管道完成一次 AddQuantInfraNet 注册并 Build 容器——此时 IRiskManager/IExecutionModel/
    /// IBinanceUsdFutureService 等已就绪；再用 BuildPipeline(sp) 现造自定义管道，手工构造 BacktestRunner 运行
    /// 它（不经过 AddQuantInfraNet 的 customStages 参数，避免 R10-fix 之前的容器时序问题，见 BuildPipeline 备注）。
    /// First completes one AddQuantInfraNet registration + Build with the default (eight-stage) pipeline — by then
    /// IRiskManager/IExecutionModel/IBinanceUsdFutureService etc. are ready; then BuildPipeline(sp) assembles the
    /// custom pipeline and a BacktestRunner is constructed by hand to run it (bypassing AddQuantInfraNet's
    /// customStages parameter, avoiding the pre-R10-fix container-sequencing bug — see BuildPipeline's remarks).
    /// </remarks>
    /// <param name="args">命令行参数（未使用）/ Command-line args (unused).</param>
    public static async Task<int> RunExampleAsync(string[] args)
    {
        var symbol = "QQQM";
        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt =>
            {
                rt.RunMode = RunMode.Backtest;
                rt.DataSource = DataSourceKind.Stooq;
            },
            o => { foreach (var (k, v) in DefaultParameters) { o.Parameters[k] = v; } });
        using var sp = services.BuildServiceProvider();

        var t0 = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ohlcvs = await sp.GetRequiredService<ITraditionalFinanceSourceDataService>()
            .DownloadOhlcvListAsync(symbol, t0, DateTime.UtcNow);
        var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            [symbol] = ohlcvs.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList(),
        });

        var pipeline = new StrategyPipeline(BuildPipeline(sp));
        var broker = (IBacktestBroker)sp.GetRequiredService<IBinanceUsdFutureService>();
        var runner = new BacktestRunner(
            pipeline, broker, sp.GetRequiredService<OrchestrationOptions>(), sp.GetRequiredService<BacktestOptions>());

        var result = await runner.RunAsync(data, new[] { symbol });
        System.Console.WriteLine($"CAGR={result.Metrics.Cagr:P2} Sharpe={result.Metrics.SharpeRatio:F2} MaxDrawdown={result.Metrics.MaxDrawdown:P2}");
        return 0;
    }

    /// <summary>
    /// QQQM 逆向 MA200 定投策略阶段：继承 <see cref="Strategy"/> 基类，只实现"自己的"信号逻辑
    /// （SMA200 + 公式 → Signal + TargetPosition）；数据装载、参数读取、事件日志、槽位契约由基类统一提供。
    /// The QQQM reverse-MA200 DCA stage: inherits the Strategy base and implements only its own signal logic
    /// (SMA200 + formula → Signal + TargetPosition); loading / params / events / slot contract come from the base.
    /// </summary>
    public sealed class QqqmReverseDcaStage : Strategy
    {
        /// <summary>
        /// 初始化策略阶段（数据装载依赖均可选：回测下 context 已注入缓存切片，走缓存路径零网络）。
        /// Initializes the stage (all loading dependencies optional: under Backtest the context already holds
        /// the cached slice, so the cache path is used with zero network).
        /// </summary>
        public QqqmReverseDcaStage(
            ITraditionalFinanceSourceDataService? yahooData,
            IBinanceUsdFutureService? binanceService)
            : base(yahooData, binanceService)
        {
        }

        /// <summary>策略名（固定 "QqqmReverseDca"）/ Strategy name (fixed "QqqmReverseDca").</summary>
        public override string StrategyName => "QqqmReverseDca";

        /// <summary>
        /// 子类核心逻辑：数据不足 → 记录事件并跳过（不产出 Signal）；否则算 SMA200 + targetWeight → Publish。
        /// Core logic: insufficient data → log + skip (no Signal); otherwise compute SMA200 + targetWeight → Publish.
        /// </summary>
        /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
        /// <param name="ct">取消令牌 / Cancellation token.</param>
        protected override async Task ExecuteCoreAsync(IPipelineContext context, CancellationToken ct)
        {
            var symbol = context.GetParameter("Symbol") ?? "QQQM";
            var maPeriod = Math.Max(2, GetInt(context, "MaPeriod", 200));
            var baseWeight = GetDouble(context, "BaseWeight", 0.5);
            var addIntensity = GetDouble(context, "AddIntensity", 1.5);
            var trimIntensity = GetDouble(context, "TrimIntensity", 1.0);
            var maxWeight = GetDouble(context, "MaxWeight", 1.0);
            var minWeight = GetDouble(context, "MinWeight", 0.0);

            // 数据装载、参数读取均走基类（SignalDataLoader 装载规则 + Orchestration.Parameters 默认值）。
            // Loading + parameter reads go through the base (SignalDataLoader rule + Orchestration.Parameters defaults).
            var closes = await LoadClosesAsync(context, symbol, ct).ConfigureAwait(false);
            if (closes.Count < maPeriod)
            {
                Log(context, $"insufficient data for '{symbol}': {closes.Count} < {maPeriod} (no signal)");
                return;
            }

            var close = closes[^1];
            var sma = closes.TakeLast(maPeriod).Average();
            var ratio = close / sma;
            var deviation = 1.0 - ratio;
            var targetWeight = ComputeTargetWeight(close, sma, baseWeight, addIntensity, trimIntensity, minWeight, maxWeight);
            var direction = targetWeight > 0 ? SignalDirection.Long : SignalDirection.Flat;
            var reason = string.Format(CultureInfo.InvariantCulture,
                "ratio={0:0.0000} deviation={1:0.0000} targetWeight={2:0.0000} (base={3:0.00} add={4:0.00} trim={5:0.00} range=[{6:0.00},{7:0.00}])",
                new object[] { ratio, deviation, targetWeight, baseWeight, addIntensity, trimIntensity, minWeight, maxWeight });

            var signal = new Signal
            {
                Symbol = symbol,
                GeneratedUtc = DateTime.UtcNow,
                Direction = direction,
                Strength = targetWeight,
                Reason = reason,
            };
            var target = new TargetPosition
            {
                Symbol = symbol,
                TargetWeight = targetWeight,
                OriginSignal = signal,
            };

            // 槽位契约由基类 Publish 统一提供（IReadOnlyList<Signal> + IReadOnlyList<TargetPosition>）。
            // The slot contract is provided uniformly by the base Publish (IReadOnlyList<Signal> + IReadOnlyList<TargetPosition>).
            Publish(context, signal, target);
        }
    }
}