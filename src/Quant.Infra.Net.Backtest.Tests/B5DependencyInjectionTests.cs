using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.Shared.Model;

namespace Quant.Infra.Net.Backtest.Tests;

/// <summary>
/// B5：<c>AddQuantInfraNetBacktest</c>（§7.7 / D1 机制）DI 契约验收。
/// B5: DI contract acceptance for AddQuantInfraNetBacktest (section 7.7 / D1 mechanism).
/// </summary>
[TestClass]
public sealed class B5DependencyInjectionTests
{
    [TestMethod]
    public void BrokerIsBacktestInstance_AndSingletonPerContainer()
    {
        using var provider = new ServiceCollection()
            .AddQuantInfraNetBacktest()
            .BuildServiceProvider();

        var first = provider.GetRequiredService<IBinanceUsdFutureService>();
        var second = provider.GetRequiredService<IBinanceUsdFutureService>();

        Assert.IsTrue(first is BacktestBrokerService, "IBinanceUsdFutureService must resolve to BacktestBrokerService");
        Assert.AreSame(first, second, "container-scoped singleton ⇒ same instance");
        Assert.AreEqual(ExchangeEnvironment.Paper, first.ExchangeEnvironment);
    }

    [TestMethod]
    public void RunnerAndPipelineResolve_Together()
    {
        using var provider = new ServiceCollection()
            .AddQuantInfraNetBacktest(
                configureBacktest: b => b.InitialEquityUsd = 25000m,
                configureOrchestration: o => o.Parameters["Strategy"] = "MaCross")
            .BuildServiceProvider();

        var runner = provider.GetRequiredService<BacktestRunner>();
        Assert.IsNotNull(runner);
        Assert.IsNotNull(provider.GetRequiredService<StrategyPipeline>());

        // 配置回调生效 / options callback takes effect
        Assert.AreEqual(25000m, provider.GetRequiredService<BacktestOptions>().InitialEquityUsd);

        // D1：broker 与 runner 共用同一实例
        // D1: broker and runner share one instance.
        var broker = provider.GetRequiredService<IBinanceUsdFutureService>();
        Assert.IsTrue(broker is BacktestBrokerService);
    }

    [TestMethod]
    public void EnvironmentIsForcedToPaper_EvenWithoutUserConfig()
    {
        using var provider = new ServiceCollection()
            .AddQuantInfraNetBacktest() // 用户什么都不配 / user configures nothing
            .BuildServiceProvider();

        var options = provider.GetRequiredService<OrchestrationOptions>();
        Assert.AreEqual(ExchangeEnvironment.Paper, options.Environment);
    }

    [TestMethod]
    public void CustomSignalGenerator_TakesPrecedence()
    {
        // 自定义信号生成器（具体实例）直接生效 / a concrete custom signal generator wins.
        var custom = new MaCrossSignalGenerator(new AnalysisService());

        using var provider = new ServiceCollection()
            .AddQuantInfraNetBacktest(customSignalGenerator: custom)
            .BuildServiceProvider();

        Assert.AreSame(custom, provider.GetRequiredService<ISignalGenerator>());
    }

    [TestMethod]
    public void DefaultStrategyFactory_ResolvedFromParameters()
    {
        using var provider = new ServiceCollection()
            .AddQuantInfraNetBacktest(configureOrchestration: o => o.Parameters["Strategy"] = "MeanReversion")
            .BuildServiceProvider();

        Assert.IsTrue(provider.GetRequiredService<ISignalGenerator>() is MeanReversionSignalGenerator);
    }
}
