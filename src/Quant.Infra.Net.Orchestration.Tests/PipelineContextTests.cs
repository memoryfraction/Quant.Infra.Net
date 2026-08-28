using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// PipelineContext 单元测试：数据槽、参数、错误/事件累积、并发冒烟。
/// PipelineContext unit tests: data slots, parameters, error/event accumulation, concurrency smoke.
/// </summary>
[TestClass]
public class PipelineContextTests
{
    /// <summary>
    /// Set/Get 应按类型存取，未存入的槽返回 null。
    /// Set/Get should round-trip per type; absent slots return null.
    /// </summary>
    [TestMethod]
    public void SetGet_RoundTrips_ByNameAndType()
    {
        var ctx = new PipelineContext(runId: 1);

        Assert.IsNull(ctx.Get<string>());

        ctx.Set("hello");
        ctx.Set(new Signal { Symbol = "BTCUSDT" });

        Assert.AreEqual("hello", ctx.Get<string>());
        Assert.AreEqual("BTCUSDT", ctx.Get<Signal>()!.Symbol);
    }

    /// <summary>
    /// 同类型后写应覆盖先写。
    /// A later write of the same type overwrites the earlier value.
    /// </summary>
    [TestMethod]
    public void Set_SameTypeSecondWrite_Overwrites()
    {
        var ctx = new PipelineContext(runId: 2);
        var first = new Signal { Symbol = "BTCUSDT" };
        var second = new Signal { Symbol = "ETHUSDT" };

        ctx.Set(first);
        ctx.Set(second);

        Assert.AreSame(second, ctx.Get<Signal>());
    }

    /// <summary>
    /// Set(null) 与 Get 的 null 边界。
    /// Set(null) boundary and null returns from Get.
    /// </summary>
    [TestMethod]
    public void Set_NullValue_ThrowsIsNull()
    {
        var ctx = new PipelineContext(runId: 3);

        object? value = null;
        Assert.ThrowsException<ArgumentNullException>(() => ctx.Set<object>(value!));
        Assert.ThrowsException<ArgumentNullException>(() => ctx.AddEvent(null!));
        Assert.ThrowsException<ArgumentNullException>(() => ctx.AddError(null!));
    }

    /// <summary>
    /// GetParameter 命中/未命中/空白键边界。
    /// GetParameter hit/miss and blank-key boundary.
    /// </summary>
    [TestMethod]
    public void GetParameter_HitMissAndNullSafety()
    {
        var ctx = new PipelineContext(runId: 4, parameters: new Dictionary<string, string> { ["symbola"] = "BTCUSDT" });

        Assert.AreEqual("BTCUSDT", ctx.GetParameter("symbola"));
        Assert.AreEqual("BTCUSDT", ctx.GetParameter("SYMBOLA"));
        Assert.IsNull(ctx.GetParameter("missing"));
        Assert.ThrowsException<ArgumentException>(() => ctx.GetParameter(" "));
    }

    /// <summary>
    /// AddError/Errors 与 AddEvent/Events 累积。
    /// AddError/Errors and AddEvent/Events accumulate.
    /// </summary>
    [TestMethod]
    public void ErrorsAndEvents_Accumulate()
    {
        var ctx = new PipelineContext(runId: 5);

        ctx.AddError(new Exception("boom"));
        ctx.AddEvent(PipelineEvent.Create(5, "Signal", "signal produced"));

        Assert.AreEqual(1, ctx.Errors.Count);
        Assert.AreEqual("boom", ctx.Errors[0].Message);
        Assert.AreEqual(2, ctx.Events.Count);   // AddError also appends a structured event + explicit AddEvent
        Assert.AreEqual("Signal", ctx.Events[1].Stage);
        Assert.AreEqual(5, ctx.Events[0].RunId);
    }

    /// <summary>
    /// 并发冒烟：多线程读写槽/参数/事件不抛异常且最终一致。
    /// Concurrency smoke: concurrent slot/parameter/event operations must not throw and must stay consistent.
    /// </summary>
    [TestMethod]
    public void Concurrency_Smoke_MultiReaderWriter()
    {
        var ctx = new PipelineContext(runId: 6, parameters: new Dictionary<string, string> { ["worker"] = "yes" });
        const int threads = 8;
        const int iterations = 500;

        Parallel.For(0, threads, t =>
        {
            for (var i = 0; i < iterations; i++)
            {
                ctx.Set(i.ToString());
                _ = ctx.Get<string>();
                _ = ctx.GetParameter("worker");
                ctx.AddEvent(PipelineEvent.Create(6, "Worker", $"t{t} i{i}"));
            }
        });

        Assert.IsNotNull(ctx.Get<string>());
        Assert.IsTrue(ctx.Events.Count >= threads * iterations);
    }
}
