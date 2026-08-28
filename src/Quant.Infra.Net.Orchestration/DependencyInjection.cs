using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Notification.Service;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Notifications;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Risk;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.Orchestration.State;
using Quant.Infra.Net.Orchestration.Stages;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration;

/// <summary>
/// 编排层依赖注入入口（设计文档 §5.8 的公开契约）。
/// Orchestration dependency-injection entry point (public contract from design §5.8).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 参数 key：策略名（PairTradingZScore | MaCross | MeanReversion）。
    /// Parameter key: strategy name (PairTradingZScore | MaCross | MeanReversion).
    /// </summary>
    public const string StrategyParameterKey = "Strategy";

    /// <summary>
    /// 默认策略名（缺失或空时使用）。
    /// Default strategy used when the parameter is missing or blank.
    /// </summary>
    public const string DefaultStrategyName = "PairTradingZScore";

    /// <summary>
    /// 将编排层服务注册到容器（§5.8）：状态/风控/通知枢纽/信号生成/执行模型/Paper 模拟 broker/
    /// 默认八阶段管道（DataIngest→Analysis→Signal→TargetPosition→Risk→Execution→PortfolioState→Notification）/PipelineRunner 宿主服务。
    /// Registers the orchestration services (design §5.8): state store / risk manager / notification hub /
    /// signal generator / execution model / Paper simulated broker / the default 8-stage pipeline / the PipelineRunner hosted service.
    /// </summary>
    /// <remarks>
    /// 非 Paper 环境必须由调用方先行注册实盘 <see cref="IBinanceUsdFutureService"/>（TryAdd 语义：已注册则不覆盖）；
    /// 未知的 <c>Parameters[\"Strategy\"]</c> 在首次解析 <see cref="ISignalGenerator"/> 时抛出 <see cref="ArgumentException"/>（启动即失败）。
    /// Non-Paper environments require the caller to register a live <see cref="IBinanceUsdFutureService"/> first (TryAdd semantics: never overrides an existing registration);
    /// an unknown <c>Parameters["Strategy"]</c> throws <see cref="ArgumentException"/> on first <see cref="ISignalGenerator"/> resolution (fail-fast at startup).
    /// </remarks>
    /// <param name="services">服务集合（不得为 null）/ Service collection (must not be null).</param>
    /// <param name="configure">可选的编排配置回调 / Optional options-configuration callback.</param>
    /// <param name="customStages">自定义阶段序列（提供后完全替代默认八阶段）/ Custom stage sequence (replaces the default eight stages entirely).</param>
    /// <param name="customSignalGenerator">自定义信号生成器（提供后替代按策略解析的内置生成器）/ Custom signal generator (replaces the strategy-resolved built-in generator).</param>
    /// <param name="customExecutionModel">自定义执行模型（提供后替代内置 <see cref="RebalanceExecutionModel"/>）/ Custom execution model (replaces the built-in <see cref="RebalanceExecutionModel"/>).</param>
    /// <returns>服务集合（链式）/ The same service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">services 为 null 时抛出 / Thrown when services is null.</exception>
    public static IServiceCollection AddQuantInfraNetOrchestration(
        this IServiceCollection services,
        Action<OrchestrationOptions>? configure = null,
        IEnumerable<IPipelineStage>? customStages = null,
        ISignalGenerator? customSignalGenerator = null,
        IExecutionModel? customExecutionModel = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddOptions();
        if (configure != null)
        {
            services.PostConfigure<OrchestrationOptions>(o => configure(o));
        }

        // 同时暴露具体配置类型（部分组件按具体类型注入，如 DefaultRiskManager）
        // Also expose the concrete options type (some components inject it directly, e.g. DefaultRiskManager).
        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<OrchestrationOptions>>().Value);

        // —— 核心单例（可被调用方先行注册替代：TryAdd 语义）/ Core singletons (caller may register first: TryAdd semantics)
        services.TryAddSingleton<IPortfolioStateStore, InMemoryPortfolioStateStore>();
        services.TryAddSingleton<IRiskManager, DefaultRiskManager>();
        services.TryAddSingleton<INotificationHub>(sp => new RoutingNotificationHub(
            sp.GetService<IDingtalkService>(),
            sp.GetService<IWeChatService>(),
            sp.GetService<IEmailService>(),
            sp.GetRequiredService<IOptions<OrchestrationOptions>>().Value));

        // —— broker：Paper 环境内置模拟实现；非 Paper 由调用方注册实盘服务 /
        //    Paper environment ships a simulated broker; non-Paper callers must register a live service.
        services.TryAddSingleton<IBinanceUsdFutureService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OrchestrationOptions>>().Value;
            if (options.Environment != ExchangeEnvironment.Paper)
            {
                throw new NotSupportedException(
                    "Non-Paper orchestration requires the caller to register a live IBinanceUsdFutureService before AddQuantInfraNetOrchestration().");
            }

            return new PaperBinanceUsdFutureService(options);
        });

        // —— 信号生成器：自定义优先，否则按 Parameters["Strategy"] 解析（未知值 fail-fast）
        //    Signal generator: custom wins; otherwise resolved by Parameters["Strategy"] (unknown value fails fast)
        services.TryAddSingleton<ISignalGenerator>(sp =>
        {
            if (customSignalGenerator != null)
            {
                return customSignalGenerator;
            }

            var options = sp.GetRequiredService<IOptions<OrchestrationOptions>>().Value;
            var analysis = sp.GetRequiredService<IAnalysisService>();
            var yahoo = sp.GetService<ITraditionalFinanceSourceDataService>();
            var broker = sp.GetRequiredService<IBinanceUsdFutureService>();

            var raw = options.Parameters.TryGetValue(StrategyParameterKey, out var s) ? s : DefaultStrategyName;
            return (raw ?? DefaultStrategyName).Trim() switch
            {
                "PairTradingZScore" => new PairTradingZScoreSignalGenerator(analysis, yahoo, broker),
                "MaCross" => new MaCrossSignalGenerator(analysis, yahoo, broker),
                "MeanReversion" => new MeanReversionSignalGenerator(analysis, yahoo, broker),
                _ => throw new ArgumentException(
                    $"Unknown Strategy '{raw}'. Supported values: PairTradingZScore | MaCross | MeanReversion.",
                    StrategyParameterKey)
            };
        });

        // —— 执行模型 / Execution model
        services.TryAddSingleton<IExecutionModel>(sp => customExecutionModel
            ?? new RebalanceExecutionModel(
                sp.GetRequiredService<IBinanceUsdFutureService>(),
                sp.GetRequiredService<IOptions<OrchestrationOptions>>().Value));

        // —— 策略管道：自定义阶段或默认八阶段（固定顺序，Notification 必须最后）
        //    Strategy pipeline: custom stages or the default eight stages (fixed order; Notification must be last)
        if (customStages != null)
        {
            services.TryAddSingleton(sp => new StrategyPipeline(customStages));
        }
        else
        {
            services.TryAddSingleton(sp => new StrategyPipeline(new IPipelineStage[]
            {
                new DataIngestStage(
                    sp.GetService<ITraditionalFinanceSourceDataService>(),
                    sp.GetRequiredService<IBinanceUsdFutureService>()),
                new AnalysisStage(),
                new SignalStage(sp.GetRequiredService<ISignalGenerator>()),
                new TargetPositionStage(sp.GetRequiredService<IOptions<OrchestrationOptions>>().Value),
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
                new NotificationStage(sp.GetRequiredService<INotificationHub>())
            }));
        }

        // —— 触发器与宿主执行器（调用方可先行注册自定义触发模式）
        //    Trigger and hosted runner (callers may register a custom trigger first)
        services.TryAddSingleton(_ => new IntervalTrigger(StartMode.NextMinute, TimeSpan.Zero));
        services.TryAddSingleton<PipelineRunner>();
        services.TryAddSingleton<IHostedService>(sp => sp.GetRequiredService<PipelineRunner>());

        return services;
    }
}
