using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Runtime.Strategies;

namespace Quant.Infra.Net.Runtime.Console.Strategies;

/// <summary>
/// R5 单文件范例：新增一个自定义策略 = 在本程序集里加一个实现 <see cref="IStrategyDescriptor"/> 的类
/// （内含一个 <see cref="ISignalGenerator"/>），不改 <c>Quant.Infra.Net.Runtime</c> 任何文件——
/// <c>AddQuantInfraNet</c> 的 strategyAssemblies 反射扫描会自动发现；
/// 用 <c>appsettings.json</c> 设 <c>Orchestration:Parameters:Strategy = "ExampleCustom"</c> 即选中本策略。
/// R5 single-file example: adding a custom strategy = one IStrategyDescriptor class in this assembly
/// (wrapping one ISignalGenerator); no file in Quant.Infra.Net.Runtime is touched — the reflection scan of
/// AddQuantInfraNet's strategyAssemblies discovers it automatically; select it via appsettings.json
/// "Orchestration:Parameters:Strategy" = "ExampleCustom".
/// </summary>
public sealed class ExampleCustomDescriptor : IStrategyDescriptor
{
    /// <summary>策略名（大小写不敏感解析目标；不得与内置 3 策略及本程序集其他描述符重名）/ Strategy name (case-insensitive target; must not collide with built-ins or other descriptors).</summary>
    public const string StrategyName = "ExampleCustom";

    /// <summary>策略名（目录解析的键）/ Strategy name (the catalog resolution key).</summary>
    public string Name => StrategyName;

    /// <summary>
    /// 创建信号生成器（仅类型解析 + 实例构造；不掺入任何 RunMode 分支逻辑，§11 护栏第 3 条）。
    /// Creates the signal generator (type resolution + construction only; no RunMode-specific logic, guardrail 3).
    /// </summary>
    /// <param name="serviceProvider">容器（不得为 null；生成器只取信号所需的最小依赖）/ Provider (must not be null; the generator takes only the minimal dependencies a signal needs).</param>
    /// <exception cref="ArgumentNullException">serviceProvider 为 null 时抛出 / Thrown when serviceProvider is null.</exception>
    public ISignalGenerator Create(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return new ExampleCustomSignalGenerator();
    }

    /// <summary>
    /// 确定性示例生成器：对 <c>Parameters["Symbol"]</c>（缺省 UNKNOWN）恒定输出一条 Long 信号。
    /// Deterministic example generator: always emits one Long signal for Parameters["Symbol"] (default UNKNOWN).
    /// </summary>
    private sealed class ExampleCustomSignalGenerator : ISignalGenerator
    {
        /// <summary>生成器 ID / Generator id.</summary>
        public string Id => StrategyName;

        /// <summary>
        /// 恒定输出一条 Long 示例信号（Reason 为英文，防乱码规范）。
        /// Always emits one Long example signal (Reason in English per the console-output convention).
        /// </summary>
        public Task<IReadOnlyList<Signal>> GenerateSignalsAsync(IPipelineContext context, CancellationToken ct)
        {
            var symbol = context.GetParameter("Symbol") ?? "UNKNOWN";
            return Task.FromResult<IReadOnlyList<Signal>>(new[]
            {
                new Signal { Symbol = symbol, GeneratedUtc = DateTime.UtcNow, Direction = SignalDirection.Long, Strength = 1d, Reason = "example custom strategy (single-file demo)" }
            });
        }
    }
}
