using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Backtest;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Orchestration;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Runtime.DataSources;
using Quant.Infra.Net.Runtime.Internal;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.Runtime.Strategies;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Service;
using System.Reflection;

namespace Quant.Infra.Net.Runtime;

/// <summary>
/// 统一运行时依赖注入入口（§7.7 全部契约）：按 <see cref="RuntimeOptions.RunMode"/> 分派到
/// <c>AddQuantInfraNetBacktest()</c> 或 <c>AddQuantInfraNetOrchestration()</c>；按 <see cref="RuntimeOptions.DataSource"/>
/// 分派到对应 <see cref="ITraditionalFinanceSourceDataService"/> 实现；按 Strategy 参数从
/// <see cref="StrategyCatalog"/>（strategyAssemblies 反射发现）解析 <see cref="ISignalGenerator"/> 并传给下层——
/// **不使用**下层各自内置的硬编码策略 switch，新增策略永不修改既有项目文件。
/// Unified runtime DI entry point (full section 7.7 contract): dispatches to AddQuantInfraNetBacktest() or
/// AddQuantInfraNetOrchestration() per RunMode; dispatches to the matching ITraditionalFinanceSourceDataService
/// per DataSource; resolves the ISignalGenerator from a StrategyCatalog (reflection-scanned strategyAssemblies)
/// and passes it down — bypassing each lower layer's hardcoded strategy switch, so adding a strategy never
/// touches an existing file.
/// </summary>
/// <remarks>
/// Testnet/Live 的"预注册真实 broker"步骤（原编排层设计的 U2）由本入口自动完成：
/// 用 RuntimeOptions 凭据构造核心库 BinanceUsdFutureService 并 AddSingleton（先于编排层 TryAdd，自动让位）。
/// The "pre-register the real broker" step (U2 from the orchestration design) is completed automatically by this
/// entry point: the core BinanceUsdFutureService is constructed from RuntimeOptions credentials and AddSingleton'd
/// before the orchestration TryAdd (which then steps aside).
/// </remarks>
public static class DependencyInjection
{
    /// <summary>
    /// 注册统一运行时（§7.7）。
    /// Registers the unified runtime (section 7.7).
    /// </summary>
    /// <param name="services">服务集合（不得为 null）/ Service collection (must not be null).</param>
    /// <param name="configureRuntime">运行时配置回调（不得为 null；RunMode/DataSource/凭据）/ Runtime options callback (must not be null; RunMode/DataSource/credentials).</param>
    /// <param name="configureOrchestration">编排配置回调（可选；Parameters 等 / Environment 由本入口按 RunMode 强制）/ Orchestration callback (optional; Parameters etc.; Environment is forced by this entry per RunMode).</param>
    /// <param name="configureBacktest">回测配置回调（仅 Backtest 模式生效）/ Backtest callback (only in Backtest mode).</param>
    /// <param name="customDataSource">Custom 数据种类的自定义实例（其他种类忽略；Custom 种类缺省 → fail-fast）/ Custom data source instance (ignored otherwise; Custom kind without it → fail-fast).</param>
    /// <param name="strategyAssemblies">要扫描发现 IStrategyDescriptor 的程序集（通常是 typeof(Program).Assembly）/ Assemblies to scan for IStrategyDescriptors (typically typeof(Program).Assembly).</param>
    /// <param name="customStages">自定义阶段序列（提供后完全替代默认八阶段；缺省 null 保持默认八阶段）/ Custom stage sequence (replaces the default eight stages; default null keeps the eight).</param>
    /// <returns>服务集合（链式）/ The same service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">services / configureRuntime 为 null / Thrown when services / configureRuntime is null.</exception>
    /// <exception cref="NotSupportedException">RunMode 为 Testnet/Live 但未配置 BinanceApiKey/BinanceApiSecret（fail-fast，绝不静默退化为 Paper）/ Thrown when Testnet/Live is selected without credentials (fail-fast; never silently degrades to Paper).</exception>
    /// <exception cref="ArgumentException">Parameters["Strategy"] 指向未知策略（fail-fast at startup，消息列出可用策略名）/ Thrown when Parameters["Strategy"] names an unknown strategy (fail-fast; message lists available names).</exception>
    /// <exception cref="InvalidOperationException">未知 RunMode 枚举值 / Unknown RunMode value.</exception>
    public static IServiceCollection AddQuantInfraNet(
        this IServiceCollection services,
        Action<RuntimeOptions> configureRuntime,
        Action<OrchestrationOptions>? configureOrchestration = null,
        Action<BacktestOptions>? configureBacktest = null,
        ITraditionalFinanceSourceDataService? customDataSource = null,
        IEnumerable<IPipelineStage>? customStages = null,
        params Assembly[] strategyAssemblies)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configureRuntime == null)
        {
            throw new ArgumentNullException(nameof(configureRuntime));
        }

        strategyAssemblies ??= Array.Empty<Assembly>();

        // —— 运行时配置（§7.7）/ Runtime options (section 7.7)
        var runtimeOptions = new RuntimeOptions();
        configureRuntime(runtimeOptions);
        services.TryAddSingleton(runtimeOptions);

        // —— Testnet/Live 凭据 fail-fast（在任何注册之前；绝不静默退化为 Paper）
        // —— Testnet/Live credential fail-fast (before any registration; never silently degrade to Paper)
        var needsCredentials = runtimeOptions.RunMode is RunMode.Testnet or RunMode.Live;
        if (needsCredentials
            && (string.IsNullOrWhiteSpace(runtimeOptions.BinanceApiKey)
                || string.IsNullOrWhiteSpace(runtimeOptions.BinanceApiSecret)))
        {
            throw new NotSupportedException(
                $"RunMode.{runtimeOptions.RunMode} requires RuntimeOptions.BinanceApiKey/BinanceApiSecret " +
                "(fail-fast by design; this never silently degrades to Paper).");
        }

        // —— 编排配置快照（读取 Strategy 参数）/ Orchestration options snapshot (to read the Strategy parameter)
        var orchestrationOptions = new OrchestrationOptions();
        configureOrchestration?.Invoke(orchestrationOptions);
        var strategyName = orchestrationOptions.Parameters
                .TryGetValue(Orchestration.DependencyInjection.StrategyParameterKey, out var requested)
                && !string.IsNullOrWhiteSpace(requested)
            ? requested
            : Orchestration.DependencyInjection.DefaultStrategyName;

        // —— 策略目录：调用方程序集 + 本程序集（内置 3 策略）/ Catalog: caller assemblies + this assembly (3 built-ins)
        var catalog = new StrategyCatalog(strategyAssemblies.Append(typeof(DependencyInjection).Assembly));
        var descriptor = catalog.Resolve(strategyName);

        // —— 经纪商实例（Testnet/Live = 真实 API；其余 = Paper 纯内存；Backtest 由 D1 覆盖）
        // —— Broker instance (Testnet/Live = real API; else Paper in-memory; Backtest is overridden by D1)
        IBinanceUsdFutureService broker = runtimeOptions.RunMode switch
        {
            RunMode.Testnet => new BinanceUsdFutureService(
                                       BrokerConfiguration.ForBroker(runtimeOptions.BinanceApiKey!, runtimeOptions.BinanceApiSecret!, ExchangeEnvironment.Testnet)),
            RunMode.Live => new BinanceUsdFutureService(
                                      BrokerConfiguration.ForBroker(runtimeOptions.BinanceApiKey!, runtimeOptions.BinanceApiSecret!, ExchangeEnvironment.Live)),
            _ => new PaperBinanceUsdFutureService(orchestrationOptions)
        };

        // —— 数据源（Yahoo/Csv 由工厂兜底 HistoricalDataSourceServiceCsv——该字段仅 MongoDBWebApi 分支使用，
        //    Yahoo 分支直连 Yahoo Chart API；Binance 取上方 broker 实例）
        // —— Data source (Yahoo/Csv fall back to HistoricalDataSourceServiceCsv inside the factory — that field is
        //    only used by the MongoDBWebApi branch, the Yahoo branch hits the Yahoo Chart API directly; Binance takes
        //    the broker instance above)
        ITraditionalFinanceSourceDataService dataSource;
        {
            var dsServices = new ServiceCollection();
            if (runtimeOptions.DataSource == DataSourceKind.Binance)
            {
                dsServices.AddSingleton<IBinanceUsdFutureService>(_ => broker);
            }

            using var dsProvider = dsServices.BuildServiceProvider();
            dataSource = DataSourceFactory.Create(runtimeOptions.DataSource, dsProvider, customDataSource);
        }

        // —— 策略实例：临时容器解析（仅类型解析 + 实例构造，§11 护栏第 3 条；不掺入任何 RunMode 逻辑）
        // —— Strategy instance: temp-container resolution (type resolution + construction only, guardrail 3;
        //    no RunMode-specific logic mixed in)
        ISignalGenerator generator;
        using (var genServices = new ServiceCollection()
                   .AddSingleton<IAnalysisService, AnalysisService>()
                   .AddSingleton<ITraditionalFinanceSourceDataService>(_ => dataSource)
                   .AddSingleton<IBinanceUsdFutureService>(_ => broker)
                   .BuildServiceProvider())
        {
            generator = descriptor.Create(genServices);
        }

        // —— 统一注册（先于下层 TryAdd ⇒ 编排层/回测层的对应 TryAdd 工厂自动让位）
        // —— Unified registration (before the lower layers' TryAdd ⇒ their TryAdd factories step aside)
        services.TryAddSingleton<IAnalysisService, AnalysisService>();
        services.TryAddSingleton<ITraditionalFinanceSourceDataService>(_ => dataSource);
        services.TryAddSingleton<ISignalGenerator>(_ => generator);

        return runtimeOptions.RunMode switch
        {
            // —— Backtest：D1 机制（BacktestBrokerService 记账 + BacktestRunner 驱动，零网络）
            // —— Backtest: D1 (BacktestBrokerService accounting + BacktestRunner driver, zero network)
            RunMode.Backtest => services.AddQuantInfraNetBacktest(configureBacktest, configureOrchestration, generator, customStages),

            // —— Paper：墙钟 PipelineRunner + PaperBinanceUsdFutureService 记账
            // —— Paper: wall-clock PipelineRunner + PaperBinanceUsdFutureService accounting
            RunMode.Paper => RegisterOrchestration(services, orchestration =>
            {
                orchestration.Environment = ExchangeEnvironment.Paper;
                configureOrchestration?.Invoke(orchestration);
            }, generator, broker, customStages),

            // —— Testnet/Live：自动预注册真实 broker（U2）+ 编排层
            // —— Testnet/Live: auto pre-register the real broker (U2) + orchestration layer
            RunMode.Testnet => RegisterOrchestration(services, orchestration =>
            {
                orchestration.Environment = ExchangeEnvironment.Testnet;
                configureOrchestration?.Invoke(orchestration);
            }, generator, broker, customStages),

            RunMode.Live => RegisterOrchestration(services, orchestration =>
            {
                orchestration.Environment = ExchangeEnvironment.Live;
                configureOrchestration?.Invoke(orchestration);
            }, generator, broker, customStages),

            _ => throw new InvalidOperationException($"Unknown RunMode: {runtimeOptions.RunMode}.")
        };
    }

    /// <summary>
    /// Paper/Testnet/Live 共享的编排层注册：真实/纸面 broker AddSingleton（先）+ AddQuantInfraNetOrchestration（后，TryAdd 让位）。
    /// Shared orchestration registration for Paper/Testnet/Live: broker AddSingleton (first) +
    /// AddQuantInfraNetOrchestration (second; its TryAdd steps aside).
    /// </summary>
    private static IServiceCollection RegisterOrchestration(
        IServiceCollection services,
        Action<OrchestrationOptions> configure,
        ISignalGenerator generator,
        IBinanceUsdFutureService broker,
        IEnumerable<IPipelineStage>? customStages = null)
    {
        services.AddSingleton<IBinanceUsdFutureService>(_ => broker);
        services.AddQuantInfraNetOrchestration(configure: configure, customSignalGenerator: generator, customStages: customStages);
        return services;
    }
}
