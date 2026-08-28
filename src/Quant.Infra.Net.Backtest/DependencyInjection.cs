using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Shared.Model;

namespace Quant.Infra.Net.Backtest;

/// <summary>
/// 回测层依赖注入入口（§7.7 / B5 契约，D1 机制）。
/// Backtest dependency-injection entry point (section 7.7 / B5 contract, D1 mechanism).
/// </summary>
/// <remarks>
/// D1 机制：先注册 <see cref="BacktestBrokerService"/> 实例作为 <see cref="IBinanceUsdFutureService"/>
/// （TryAdd 语义 ⇒ 后续 <c>AddQuantInfraNetOrchestration()</c> 的默认 Paper 工厂自动让位）；
/// 编排层强制 <c>Environment = Paper</c>（回测离线）；驱动器注册为 <see cref="BacktestRunner"/>，
/// **不**启动墙钟 <c>PipelineRunner</c>/触发器宿主服务（回测由调用方同步驱动）。
/// D1: the BacktestBrokerService instance is registered FIRST as IBinanceUsdFutureService
/// (TryAdd semantics make the orchestration default Paper factory step aside); the orchestration
/// environment is forced to Paper (offline); the driver is the BacktestRunner — the wall-clock
/// PipelineRunner/trigger hosted services are never started (the caller drives each run explicitly).
/// </remarks>
public static class DependencyInjection
{
    /// <summary>
    /// 注册回测层（§7.7 / B5）：BacktestBrokerService 记账 + 编排八阶段管道 + BacktestRunner 驱动。
    /// Registers the backtest layer (section 7.7 / B5): BacktestBrokerService accounting + the eight-stage
    /// orchestration pipeline + the BacktestRunner driver.
    /// </summary>
    /// <param name="services">服务集合（不得为 null）/ Service collection (must not be null).</param>
    /// <param name="configureBacktest">回测配置回调（初始权益/手续费/滑点/FillTiming 等）/ Backtest options callback (equity / costs / slippage / FillTiming).</param>
    /// <param name="configureOrchestration">编排配置回调（策略参数等；Environment 会被覆盖为 Paper）/ Orchestration callback (strategy parameters etc.; Environment is forced to Paper).</param>
    /// <param name="customSignalGenerator">自定义信号生成器（提供后替代按 Parameters["Strategy"] 解析的内置生成器）/ Custom signal generator (overrides the Parameters["Strategy"]-resolved built-in).</param>
    /// <returns>服务集合（链式）/ The same service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">services 为 null 时抛出 / Thrown when services is null.</exception>
    public static IServiceCollection AddQuantInfraNetBacktest(
        this IServiceCollection services,
        Action<BacktestOptions>? configureBacktest = null,
        Action<OrchestrationOptions>? configureOrchestration = null,
        ISignalGenerator? customSignalGenerator = null)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        // 回测配置实例：broker 与 runner 共用同一份 / One BacktestOptions instance shared by broker and runner.
        var backtestOptions = new BacktestOptions();
        configureBacktest?.Invoke(backtestOptions);
        services.TryAddSingleton(backtestOptions);

        // D1：先落 broker 实例 ⇒ 后续编排层的默认 Paper 工厂（TryAdd）自动让位。
        // D1: broker instance first ⇒ the orchestration default Paper factory (TryAdd) steps aside.
        var broker = new BacktestBrokerService(backtestOptions);
        services.AddSingleton(broker);
        services.AddSingleton<IBinanceUsdFutureService>(_ => broker);

        // 策略构造依赖：IAnalysisService（编排层不自动注册；调用方可先行注册自定义实现，TryAdd 语义）。
        // Strategy construction dependency: IAnalysisService (orchestration does not register it;
        // callers may pre-register a custom implementation — TryAdd semantics honored).
        services.TryAddSingleton<IAnalysisService, AnalysisService>();

        // 编排层：强制 Paper（回测离线，绝不碰真实 API）+ 用户配置 + 可选自定义信号生成器。
        // Orchestration: force Paper (offline, never touches real APIs) + user config + optional custom signal generator.
        services.AddQuantInfraNetOrchestration(
            configure: o =>
            {
                o.Environment = ExchangeEnvironment.Paper;
                configureOrchestration?.Invoke(o);
            },
            customSignalGenerator: customSignalGenerator);

        // 驱动器：事件驱动回放（非墙钟宿主服务）/ Driver: event-driven replay (NOT a wall-clock hosted service).
        services.TryAddSingleton(sp => new BacktestRunner(
            sp.GetRequiredService<StrategyPipeline>(),
            broker,
            sp.GetRequiredService<OrchestrationOptions>(),
            sp.GetRequiredService<BacktestOptions>()));

        return services;
    }
}
