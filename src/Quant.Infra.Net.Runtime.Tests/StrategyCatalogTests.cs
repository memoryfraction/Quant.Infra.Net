using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.Runtime.Console.Strategies;
using Quant.Infra.Net.Runtime.Strategies;
using Quant.Infra.Net.Runtime.Tests.Fixtures;
using System.Reflection;

namespace Quant.Infra.Net.Runtime.Tests;

/// <summary>
/// R1 验收测试：策略目录反射发现 / 大小写不敏感解析 / 未知名 fail-fast / 重名 fail-fast / 跨程序集自定义策略发现。
/// R1 acceptance tests: reflection discovery / case-insensitive resolution / unknown-name fail-fast /
/// duplicate-name fail-fast / cross-assembly custom descriptor discovery.
/// </summary>
[TestClass]
public class StrategyCatalogTests
{
    /// <summary>R1 验收 ①：只扫描内置程序集时必须恰好发现内置 3 个策略 / Scanning only the built-in assembly finds exactly the 3 built-ins.</summary>
    [TestMethod]
    public void BuiltIn_Three_Strategies_Are_Discovered()
    {
        var catalog = new StrategyCatalog(Array.Empty<Assembly>());

        CollectionAssert.AreEquivalent(
            new[] { MaCrossSignalGenerator.GeneratorId, MeanReversionSignalGenerator.GeneratorId, PairTradingZScoreSignalGenerator.GeneratorId },
            catalog.Names.ToArray());
    }

    /// <summary>R1 验收 ②：解析大小写不敏感 / Resolution is case-insensitive.</summary>
    [TestMethod]
    public void Resolve_Is_CaseInsensitive()
    {
        var catalog = new StrategyCatalog(Array.Empty<Assembly>());

        Assert.AreSame(catalog.Resolve("MaCross"), catalog.Resolve("macross"));
        Assert.AreEqual(MeanReversionSignalGenerator.GeneratorId, catalog.Resolve("meanreVERSION").Name);
        Assert.AreEqual(PairTradingZScoreSignalGenerator.GeneratorId, catalog.Resolve("PAIRTRADINGZSCORE").Name);
    }

    /// <summary>
    /// R1 验收 ③：未知策略抛出 ArgumentException，且信息必须列出全部可用策略名。
    /// Unknown strategy throws ArgumentException listing every available name.
    /// </summary>
    [TestMethod]
    public void Resolve_Unknown_Name_Throws_And_Lists_Available_Strategies()
    {
        var catalog = new StrategyCatalog(Array.Empty<Assembly>());
        var ex = Assert.ThrowsException<ArgumentException>(() => catalog.Resolve("Nope"));

        foreach (var name in catalog.Names)
        {
            Assert.IsTrue(ex.Message.Contains(name), $"message must list '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// R1 验收 ④：同一 Name 被两个描述符占用（本程序集内的两个类型；扫描逻辑对"任意程序集集合出现重名"一视同仁）→ 构造即 fail-fast。
    /// Two descriptors sharing one Name → construction fails fast.
    /// </summary>
    [TestMethod]
    public void Duplicate_Names_Fail_Fast_At_Construction()
    {
        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => new StrategyCatalog(new[] { typeof(DuplicateNameFixtureDescriptorA).Assembly }));

        Assert.IsTrue(ex.Message.Contains(DuplicateNameFixtureDescriptorA.SharedName), ex.Message);
    }

    /// <summary>
    /// R1 验收 ⑤：新增自定义策略描述符（位于独立程序集 Runtime.Console），不改 Runtime 任何文件即可被发现，且可创建可用生成器。
    /// A custom descriptor in a separate assembly is discoverable and functional without touching any Runtime file.
    /// </summary>
    [TestMethod]
    public void Custom_Descriptor_In_Separate_Assembly_Is_Discovered_And_Creatable()
    {
        var catalog = new StrategyCatalog(new[] { typeof(FixtureEchoDescriptor).Assembly });

        Assert.IsTrue(catalog.Names.Contains(FixtureEchoDescriptor.FixtureName), string.Join(" | ", catalog.Names));
        var generator = catalog.Resolve(FixtureEchoDescriptor.FixtureName.ToUpperInvariant()).Create(ServiceProvider());
        var signals = generator
            .GenerateSignalsAsync(
                new PipelineContext(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
                CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(SignalDirection.Long, signals[0].Direction);
        Assert.AreEqual("fixture echo signal (test discovery)", signals[0].Reason);
    }

    /// <summary>
    /// 内置描述符 Create(sp) 必须装配出与编排层 DI 工厂同型的生成器（同 Id）/ Built-in Create(sp) yields the same generator id as the orchestration DI factory.
    /// </summary>
    [TestMethod]
    public void BuiltIn_Create_Produces_Generator_Of_Expected_Id()
    {
        var catalog = new StrategyCatalog(Array.Empty<Assembly>());
        var sp = ServiceProvider();

        Assert.AreEqual(MaCrossSignalGenerator.GeneratorId, catalog.Resolve("MaCross").Create(sp).Id);
        Assert.AreEqual(MeanReversionSignalGenerator.GeneratorId, catalog.Resolve("MeanReversion").Create(sp).Id);
        Assert.AreEqual(PairTradingZScoreSignalGenerator.GeneratorId, catalog.Resolve("PairTradingZScore").Create(sp).Id);
    }

    /// <summary>参数校验：null 程序集集合 → ArgumentNullException（§11.8）/ Parameter validation: null collection → ArgumentNullException.</summary>
    [TestMethod]
    public void Constructor_Null_Assemblies_Throws()
        => Assert.ThrowsException<ArgumentNullException>(() => new StrategyCatalog(null!));

    /// <summary>参数校验：Resolve(null) → ArgumentNullException / Resolve(null) → ArgumentNullException.</summary>
    [TestMethod]
    public void Resolve_Null_Name_Throws()
        => Assert.ThrowsException<ArgumentNullException>(() => new StrategyCatalog(Array.Empty<Assembly>()).Resolve(null));

    /// <summary>参数校验：Resolve(空白) → ArgumentException / Resolve(blank) → ArgumentException.</summary>
    [TestMethod]
    public void Resolve_Blank_Name_Throws()
        => Assert.ThrowsException<ArgumentException>(() => new StrategyCatalog(Array.Empty<Assembly>()).Resolve("   "));

    private static IServiceProvider ServiceProvider()
        => new ServiceCollection()
            .AddSingleton<IAnalysisService, AnalysisService>()
            .AddSingleton<IBinanceUsdFutureService>(_ => new PaperBinanceUsdFutureService(null))
            .BuildServiceProvider();
}
