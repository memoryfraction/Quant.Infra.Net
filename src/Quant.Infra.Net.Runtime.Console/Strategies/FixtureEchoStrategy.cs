using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Runtime.Strategies;

namespace Quant.Infra.Net.Runtime.Console.Strategies;

/// <summary>
/// 测试 fixture：自定义策略描述符（验证"新增描述符无需改动 Runtime 任何文件即可被发现"，设计 R1 验收 ⑤）。
/// 也作为跨程序集重名探测的另一侧（见 StrategyCatalogTests.Duplicate_Names_Fail_Fast）。
/// Test fixture: a custom strategy descriptor proving new descriptors are discovered without touching any Runtime file
/// (acceptance R1 item 5), and the other side of the cross-assembly duplicate-name probe.
/// </summary>
public sealed class FixtureEchoDescriptor : IStrategyDescriptor
{
    /// <summary>fixture 策略名 / Fixture strategy name.</summary>
    public const string FixtureName = "__fixture_echo__";

    /// <summary>策略名（大小写不敏感解析目标）/ Strategy name (the case-insensitive resolution target).</summary>
    public string Name => FixtureName;

    /// <summary>创建 fixture 信号生成器（零依赖）/ Creates the fixture generator (zero dependencies).</summary>
    /// <param name="serviceProvider">容器（fixture 不访问）/ Provider (unused by the fixture).</param>
    public ISignalGenerator Create(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return new FixtureEchoSignalGenerator();
    }

    /// <summary>固定的常量信号生成器（确定性返回一条 Long 信号）/ Fixed constant generator (deterministically returns one Long signal).</summary>
    private sealed class FixtureEchoSignalGenerator : ISignalGenerator
    {
        /// <summary>生成器 ID / Generator id.</summary>
        public string Id => FixtureName;

        /// <summary>恒定返回一条 Long fixture 信号 / Always returns one Long fixture signal.</summary>
        public Task<IReadOnlyList<Signal>> GenerateSignalsAsync(IPipelineContext context, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Signal>>(new[]
            {
                new Signal { Symbol = "FIXTURE", GeneratedUtc = DateTime.UtcNow, Direction = SignalDirection.Long, Strength = 1d, Reason = "fixture echo signal (test discovery)" }
            });
    }
}
