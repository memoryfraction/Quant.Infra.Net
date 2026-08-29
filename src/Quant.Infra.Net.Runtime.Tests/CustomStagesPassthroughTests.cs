using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.Models;

namespace Quant.Infra.Net.Runtime.Tests;

/// <summary>
/// R7 验收：customStages 经统一入口 AddQuantInfraNet → AddQuantInfraNetBacktest → AddQuantInfraNetOrchestration
/// 逐层透传后，最终装配出的 StrategyPipeline.Stages 就是调用方传入的序列（默认八阶段被完全替代）。
/// R7 acceptance: customStages threaded AddQuantInfraNet → AddQuantInfraNetBacktest → AddQuantInfraNetOrchestration
/// yields a StrategyPipeline whose Stages are exactly the caller-supplied sequence (default eight replaced).
/// </summary>
[TestClass]
public sealed class CustomStagesPassthroughTests
{
    /// <summary>
    /// 最小自定义阶段序列（2 个空操作阶段）透传后，最终容器里的管道恰好就是这 2 个阶段
    /// （引用级一致，而非默认八阶段）。
    /// A minimal custom stage sequence (two no-op stages) threaded through the unified entry ends up as exactly
    /// those two stages in the assembled pipeline (reference-identical, not the default eight).
    /// </summary>
    [TestMethod]
    public void CustomStages_Passthrough_Replace_Default_Eight_Stage_Pipeline()
    {
        var customStages = new IPipelineStage[]
        {
            new NoopStage("S1"),
            new NoopStage("S2"),
        };

        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt => rt.RunMode = RunMode.Backtest,
            customStages: customStages);
        using ServiceProvider sp = services.BuildServiceProvider();

        var pipeline = sp.GetRequiredService<StrategyPipeline>();
        var stages = pipeline.Stages;

        // 完全替代默认八阶段：数量、顺序、引用三者一致。
        // Completely replaces the default eight: count, order, and reference identity all match.
        Assert.AreEqual(customStages.Length, stages.Count, "custom stage sequence must fully replace the default eight");
        foreach (var (expected, actual) in customStages.Zip(stages))
        {
            Assert.AreSame(expected, actual, $"stage '{expected.Name}' must be the very instance passed in");
        }
    }

    /// <summary>
    /// 测试用空操作阶段 / no-op stage for tests.
    /// </summary>
    private sealed class NoopStage : IPipelineStage
    {
        internal NoopStage(string name) => Name = name;

        public string Name { get; }

        public Task ExecuteAsync(IPipelineContext context, CancellationToken ct) => Task.CompletedTask;
    }
}
