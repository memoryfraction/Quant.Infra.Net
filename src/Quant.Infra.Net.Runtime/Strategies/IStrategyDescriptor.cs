using Quant.Infra.Net.Orchestration.Abstractions;

namespace Quant.Infra.Net.Runtime.Strategies;

/// <summary>
/// 策略描述符：把一个 <see cref="ISignalGenerator"/> 实现登记为可按名字解析的策略（设计 §7.6）。
/// 约定：自定义策略一个 .cs 文件，ISignalGenerator 实现与其 IStrategyDescriptor 实现写在同一文件（内置 3 个策略除外，见 U4）。
/// Strategy descriptor: registers one ISignalGenerator implementation as resolvable by name (design section 7.6).
/// Convention: one file per strategy, with the ISignalGenerator and its IStrategyDescriptor co-located
/// in the same file (the 3 built-ins are the sole exception, see U4).
/// </summary>
public interface IStrategyDescriptor
{
    /// <summary>
    /// 策略名（对应 appsettings.json 里 Orchestration.Parameters.Strategy 的取值，大小写不敏感）。
    /// Strategy name (matches Orchestration.Parameters.Strategy, case-insensitive).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 创建该策略的信号生成器实例（依赖从统一容器解析，与 AddQuantInfraNetOrchestration 的 ISignalGenerator 工厂一致）。
    /// Creates the signal generator instance for this strategy (dependencies resolved from the unified container,
    /// mirroring the AddQuantInfraNetOrchestration ISignalGenerator factory).
    /// </summary>
    /// <param name="serviceProvider">统一容器（不得为 null）/ Unified service provider (must not be null).</param>
    /// <returns>信号生成器实例 / The signal generator instance.</returns>
    ISignalGenerator Create(IServiceProvider serviceProvider);
}
