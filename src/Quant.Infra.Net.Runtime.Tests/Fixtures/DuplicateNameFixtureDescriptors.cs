using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Runtime.Strategies;

namespace Quant.Infra.Net.Runtime.Tests.Fixtures;

/// <summary>
/// 测试 fixture A：与 <see cref="DuplicateNameFixtureDescriptorB"/> 同名（验证跨类重名 fail-fast，R1 验收 ④）。
/// Test fixture A: same name as DuplicateNameFixtureDescriptorB (duplicate-name fail-fast probe, R1 item 4).
/// </summary>
public sealed class DuplicateNameFixtureDescriptorA : IStrategyDescriptor
{
    /// <summary>与 fixture B 相同的策略名 / Same strategy name as fixture B.</summary>
    public const string SharedName = "__duplicate_name_fixture__";

    /// <summary>策略名（与 B 重名）/ Strategy name (duplicate of B).</summary>
    public string Name => SharedName;

    /// <summary>创建（fixture 不被调用）/ Not invoked by the probe.</summary>
    public ISignalGenerator Create(IServiceProvider serviceProvider)
        => throw new NotSupportedException("duplicate-name fixture must never be created");
}

/// <summary>
/// 测试 fixture B：与 <see cref="DuplicateNameFixtureDescriptorA"/> 同名。
/// Test fixture B: same name as DuplicateNameFixtureDescriptorA.
/// </summary>
public sealed class DuplicateNameFixtureDescriptorB : IStrategyDescriptor
{
    /// <summary>与 fixture A 相同的策略名 / Same strategy name as fixture A.</summary>
    public string Name => DuplicateNameFixtureDescriptorA.SharedName;

    /// <summary>创建（fixture 不被调用）/ Not invoked by the probe.</summary>
    public ISignalGenerator Create(IServiceProvider serviceProvider)
        => throw new NotSupportedException("duplicate-name fixture must never be created");
}
