using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.DataSources;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Runtime.Tests.DependencyInjection;

/// <summary>
/// R3 验收：四种 RunMode 分别断言统一容器里解析出的关键服务类型
/// （Backtest→BacktestBrokerService/BacktestRunner；Paper→PaperBinanceUsdFutureService/PipelineRunner；
/// Testnet/Live→核心库真实 broker + 正确 ExchangeEnvironment；Testnet/Live 无凭据 → NotSupportedException）。
/// R3 acceptance: for each of the 4 RunModes assert the key service types resolved from the unified container.
/// </summary>
[TestClass]
public class RunModeDispatchTests
{
    /// <summary>Backtest：经纪商=BacktestBrokerService（D1），驱动器=BacktestRunner，默认策略 PairTradingZScore / Backtest: broker=BacktestBrokerService (D1), driver=BacktestRunner, default strategy PairTradingZScore.</summary>
    [TestMethod]
    public void Backtest_Mode_Resolves_BacktestBroker_And_Runner()
    {
        using var sp = BuildContainer(
            mode: RunMode.Backtest,
            parameters: new Dictionary<string, string> { ["Symbol"] = "AAPL" });

        Assert.IsInstanceOfType(sp.GetRequiredService<IBinanceUsdFutureService>(), typeof(BacktestBrokerService));
        Assert.IsNotNull(sp.GetRequiredService<BacktestRunner>());
        Assert.AreEqual("PairTradingZScore", sp.GetRequiredService<ISignalGenerator>().Id);
    }

    /// <summary>Backtest：Parameters["Strategy"]=MaCross → 解析出 MaCross 生成器 / Backtest: Parameters["Strategy"]=MaCross selects the MaCross generator.</summary>
    [TestMethod]
    public void Backtest_Mode_Parameters_Strategy_Selects_Generator()
    {
        using var sp = BuildContainer(
            mode: RunMode.Backtest,
            parameters: new Dictionary<string, string>
            {
                ["Symbol"] = "AAPL",
                ["Strategy"] = "MaCross"
            });

        Assert.AreEqual("MaCross", sp.GetRequiredService<ISignalGenerator>().Id);
    }

    /// <summary>Paper：经纪商=PaperBinanceUsdFutureService，驱动器=PipelineRunner（墙钟）/ Paper: broker=PaperBinanceUsdFutureService, driver=PipelineRunner (wall-clock).</summary>
    [TestMethod]
    public void Paper_Mode_Resolves_PaperBroker_And_PipelineRunner()
    {
        using var sp = BuildContainer(
            mode: RunMode.Paper,
            parameters: new Dictionary<string, string> { ["Symbol"] = "AAPL" });

        Assert.IsInstanceOfType(sp.GetRequiredService<IBinanceUsdFutureService>(), typeof(Quant.Infra.Net.Orchestration.Execution.PaperBinanceUsdFutureService));
        Assert.IsNotNull(sp.GetRequiredService<PipelineRunner>());
        Assert.IsNotNull(sp.GetRequiredService<ISignalGenerator>());
        Assert.IsNotNull(sp.GetRequiredService<ITraditionalFinanceSourceDataService>());
    }

    /// <summary>Testnet：凭据齐全 → 核心库真实 broker + ExchangeEnvironment.Testnet / Testnet: with credentials → core real broker + ExchangeEnvironment.Testnet.</summary>
    [TestMethod]
    public void Testnet_Mode_Resolves_RealBroker_With_Testnet_Environment()
    {
        using var sp = BuildContainer(
            mode: RunMode.Testnet,
            parameters: new Dictionary<string, string> { ["Symbol"] = "AAPL" });

        var broker = sp.GetRequiredService<IBinanceUsdFutureService>();
        Assert.IsInstanceOfType(broker, typeof(BinanceUsdFutureService));
        Assert.AreEqual(Quant.Infra.Net.Shared.Model.ExchangeEnvironment.Testnet, broker.ExchangeEnvironment);
    }

    /// <summary>Live：凭据齐全 → 核心库真实 broker + ExchangeEnvironment.Live / Live: with credentials → core real broker + ExchangeEnvironment.Live.</summary>
    [TestMethod]
    public void Live_Mode_Resolves_RealBroker_With_Live_Environment()
    {
        using var sp = BuildContainer(
            mode: RunMode.Live,
            parameters: new Dictionary<string, string> { ["Symbol"] = "AAPL" });

        var broker = sp.GetRequiredService<IBinanceUsdFutureService>();
        Assert.IsInstanceOfType(broker, typeof(BinanceUsdFutureService));
        Assert.AreEqual(Quant.Infra.Net.Shared.Model.ExchangeEnvironment.Live, broker.ExchangeEnvironment);
    }

    /// <summary>R3 验收：Testnet 无凭据 → NotSupportedException（fail-fast，在任何注册之前）/ Testnet without credentials throws NotSupportedException (fail-fast).</summary>
    [TestMethod]
    public void Testnet_Without_Credentials_Throws_NotSupportedException()
        => Assert.ThrowsException<NotSupportedException>(
            () => BuildContainer(RunMode.Testnet, new Dictionary<string, string>(), withCredentials: false));

    /// <summary>R3 验收：Live 无凭据 → NotSupportedException / Live without credentials throws NotSupportedException.</summary>
    [TestMethod]
    public void Live_Without_Credentials_Throws_NotSupportedException()
        => Assert.ThrowsException<NotSupportedException>(
            () => BuildContainer(RunMode.Live, new Dictionary<string, string>(), withCredentials: false));

    /// <summary>Testnet 仅有 Key 缺 Secret → 仍 NotSupportedException（双键缺一不可）/ Testnet with key but no secret still throws (both are required).</summary>
    [TestMethod]
    public void Testnet_With_Key_Only_Still_Throws()
    {
        var services = new ServiceCollection();
        Assert.ThrowsException<NotSupportedException>(
            () => services.AddQuantInfraNet(
                rt =>
                {
                    rt.RunMode = RunMode.Testnet;
                    rt.BinanceApiKey = "has-key-only";
                }));
    }

    /// <summary>未知策略名 → 启动期 ArgumentException（fail-fast，消息列出可用策略）/ Unknown strategy name throws ArgumentException at registration with an available-name listing.</summary>
    [TestMethod]
    public void Unknown_Strategy_Name_Fails_Fast_At_Add()
    {
        var services = new ServiceCollection();
        var ex = Assert.ThrowsException<ArgumentException>(
            () => services.AddQuantInfraNet(
                rt => rt.RunMode = RunMode.Paper,
                configureOrchestration: o => o.Parameters["Strategy"] = "NoSuchStrategy"));
        StringAssert.Contains(ex.Message, "MaCross");
    }

    /// <summary>Custom 数据源实例原样流入统一容器（Paper 模式）/ Custom data source instance flows into the unified container (Paper mode).</summary>
    [TestMethod]
    public void Custom_DataSource_Flows_Into_Container()
    {
        var custom = new DemoSyntheticSourceDataService();
        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt =>
            {
                rt.RunMode = RunMode.Paper;
                rt.DataSource = DataSourceKind.Custom;
            },
            configureOrchestration: o => o.Parameters["Symbol"] = "AAPL",
            customDataSource: custom);
        using ServiceProvider sp = services.BuildServiceProvider();

        Assert.AreSame(custom, sp.GetRequiredService<ITraditionalFinanceSourceDataService>());
    }

    /// <summary>策略构造依赖 IAnalysisService 可用（Paper 路径必须，B5 教训）/ The strategy construction dependency IAnalysisService is resolvable (required for the Paper path).</summary>
    [TestMethod]
    public void AnalysisService_Is_Resolvable_In_All_Modes()
    {
        using (var sp = BuildContainer(RunMode.Backtest, new Dictionary<string, string>()))
        {
            Assert.IsNotNull(sp.GetRequiredService<IAnalysisService>());
        }

        using (var sp2 = BuildContainer(RunMode.Paper, new Dictionary<string, string>()))
        {
            Assert.IsNotNull(sp2.GetRequiredService<IAnalysisService>());
        }
    }

    private static ServiceProvider BuildContainer(
        RunMode mode,
        IReadOnlyDictionary<string, string> parameters,
        bool withCredentials = true)
    {
        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt =>
            {
                rt.RunMode = mode;
                if (withCredentials && mode is RunMode.Testnet or RunMode.Live)
                {
                    // 测试环境：仅构造 broker 实例，绝不触发网络调用 / Test only: constructor runs; no network calls are ever made.
                    rt.BinanceApiKey = "testnet-key-placeholder";
                    rt.BinanceApiSecret = "testnet-secret-placeholder";
                }
            },
            configureOrchestration: o =>
            {
                foreach (var kv in parameters)
                {
                    o.Parameters[kv.Key] = kv.Value;
                }
            });
        return services.BuildServiceProvider();
    }
}
