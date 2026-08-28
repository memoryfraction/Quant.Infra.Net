using Quant.Infra.Net.Shared.Model;

namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 编排层配置（DI 绑定到 "Orchestration" 配置节）。
/// Orchestration configuration (bound to the "Orchestration" configuration section).
/// </summary>
public class OrchestrationOptions
{
    /// <summary>
    /// 运行环境；默认 Paper（纯内存，绝不触实盘）。
    /// Runtime environment; defaults to Paper (pure in-memory, never touches live trading).
    /// </summary>
    public ExchangeEnvironment Environment { get; set; } = ExchangeEnvironment.Paper;

    /// <summary>
    /// Paper 环境起始权益（USD）。
    /// Initial equity for Paper environment (USD).
    /// </summary>
    public decimal InitialEquityUsd { get; set; } = 10000m;

    /// <summary>
    /// 单标的最大权重（风控规则 1 的阈值）。
    /// Maximum weight per symbol (threshold of risk rule 1).
    /// </summary>
    public double MaxWeightPerSymbol { get; set; } = 0.5;

    /// <summary>
    /// 总敞口上限 Σ|w|（风控规则 2 的阈值）。
    /// Total gross exposure cap Σ|w| (threshold of risk rule 2).
    /// </summary>
    public double MaxGrossExposure { get; set; } = 2.0;

    /// <summary>
    /// Kill-switch 回撤阈值（负数；风控规则 3，低于该值即触发）。
    /// Kill-switch drawdown threshold (negative; risk rule 3, triggered when equity drawdown is at or below it).
    /// </summary>
    public double KillSwitchDrawdownRate { get; set; } = -0.20;

    /// <summary>
    /// 调仓死区 |target − actual| &lt; 该值则跳过（避免高频微调）。
    /// Rebalance dead zone: skip when |target − actual| is below this value (avoids high-frequency micro-adjustments).
    /// </summary>
    public double MinRebalanceDelta { get; set; } = 0.02;

    /// <summary>
    /// 策略参数（键值由策略自己解释，如 SymbolA/SymbolB、LookbackBars）。
    /// Strategy parameters (keys interpreted by the strategy itself, e.g., SymbolA/SymbolB, LookbackBars).
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 通知路由配置（钉钉 / 企微 / 邮件；详见 §5.6）。
    /// Notification routing configuration (DingTalk / WeChat Work / Email; see design §5.6).
    /// </summary>
    public NotificationOptions Notifications { get; set; } = new();
}
