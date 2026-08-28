using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// StrategyPipeline 单元测试：顺序、abort 提前终止、fatal 记录、命名校验。
/// StrategyPipeline unit tests: ordering, abort early-termination, fatal recording, name validation.
/// </summary>
[TestClass]
public class StrategyPipelineTests
{
    /// <summary>
    /// 阶段应按注册顺序执行。
    /// Stages must execute in registration order.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_ExecutesStages_InRegistrationOrder()
    {
        var order = new List<string>();
        var pipeline = new StrategyPipeline(new IPipelineStage[]
        {
            new RecordingStage("A", order),
            new RecordingStage("B", order),
            new RecordingStage("C", order)
        });

        await pipeline.RunAsync(new PipelineContext(1), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "A", "B", "C" }, order);
    }

    /// <summary>
    /// 任一阶段抛出 PipelineAbortException 后，后续阶段不得执行，异常重抛。
    /// After a stage raises PipelineAbortException, later stages must not run and the exception is rethrown.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_AbortException_StopsLaterStages_AndRethrows()
    {
        var order = new List<string>();
        var pipeline = new StrategyPipeline(new IPipelineStage[]
        {
            new RecordingStage("A", order),
            new ThrowingStage("B", new PipelineAbortException("risk rejected")),
            new RecordingStage("C", order)
        });
        var ctx = new PipelineContext(2);

        var ex = await Assert.ThrowsExceptionAsync<PipelineAbortException>(
            () => pipeline.RunAsync(ctx, CancellationToken.None));

        CollectionAssert.AreEqual(new[] { "A" }, order);
        StringAssert.Contains(ex.Message, "risk rejected");
        Assert.IsTrue(ctx.Events.Any(e => e.Message.Contains("aborted")));
    }

    /// <summary>
    /// 非 abort 异常应记入 Errors、终止后续阶段并重抛。
    /// A non-abort exception must be recorded in Errors, stop later stages, and be rethrown.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_FatalException_LogsError_StopsLaterStages_AndRethrows()
    {
        var order = new List<string>();
        var fatal = new InvalidOperationException("broker down");
        var pipeline = new StrategyPipeline(new IPipelineStage[]
        {
            new RecordingStage("A", order),
            new ThrowingStage("B", fatal),
            new RecordingStage("C", order)
        });
        var ctx = new PipelineContext(3);

        var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => pipeline.RunAsync(ctx, CancellationToken.None));

        CollectionAssert.AreEqual(new[] { "A" }, order);
        Assert.AreSame(fatal, ctx.Errors[0]);
        Assert.IsTrue(ctx.Events.Any(e => e.Message.Contains("run failed")));
        StringAssert.Contains(ex.Message, "broker down");
    }

    /// <summary>
    /// 构造校验：null 集合、空名、重名都应抛异常。
    /// Construction validation: null collection, blank name, and duplicate name must all throw.
    /// </summary>
    [TestMethod]
    public void Ctor_InvalidStages_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new StrategyPipeline(null!));
        Assert.ThrowsException<ArgumentException>(() => new StrategyPipeline(new IPipelineStage[]
        {
            new RecordingStage("  ", new List<string>())
        }));
        Assert.ThrowsException<ArgumentException>(() => new StrategyPipeline(new IPipelineStage[]
        {
            new RecordingStage("A", new List<string>()),
            new RecordingStage("A", new List<string>())
        }));
        Assert.ThrowsException<ArgumentException>(() => new StrategyPipeline(new IPipelineStage[] { null! }));
    }

    /// <summary>
    /// RunAsync(null context) 应抛出 ArgumentNullException。
    /// RunAsync(null context) should throw ArgumentNullException.
    /// </summary>
    [TestMethod]
    public void RunAsync_NullContext_Throws()
    {
        var pipeline = new StrategyPipeline(Array.Empty<IPipelineStage>());
        Assert.ThrowsException<ArgumentNullException>(() => pipeline.RunAsync(null!, CancellationToken.None).GetAwaiter().GetResult());
    }

    private sealed class RecordingStage : IPipelineStage
    {
        public RecordingStage(string name, List<string> order)
        {
            Name = name;
            _order = order;
        }

        private readonly List<string> _order;

        public string Name { get; }

        public Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
        {
            lock (_order)
            {
                _order.Add(Name);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStage : IPipelineStage
    {
        public ThrowingStage(string name, Exception ex)
        {
            Name = name;
            _ex = ex;
        }

        private readonly Exception _ex;

        public string Name { get; }

        public Task ExecuteAsync(IPipelineContext context, CancellationToken ct) => throw _ex;
    }
}
