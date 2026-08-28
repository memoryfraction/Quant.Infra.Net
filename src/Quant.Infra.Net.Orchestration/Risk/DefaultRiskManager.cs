using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Risk;

/// <summary>
/// 默认风控管理器：按序检查三条规则，任一失败即拒绝并列出全部原因。
/// Default risk manager: checks three rules in order; any failure rejects and lists all reasons.
/// 规则顺序：1) 单标的 |w| &lt;= MaxWeightPerSymbol；2) Σ|w| &lt;= MaxGrossExposure；
/// 3) 当前 UnrealizedProfitRate &gt; KillSwitchDrawdownRate（附带"建议全部平仓"原因）。
/// Rule order: 1) |w| per symbol &lt;= MaxWeightPerSymbol; 2) Σ|w| &lt;= MaxGrossExposure;
/// 3) UnrealizedProfitRate &gt; KillSwitchDrawdownRate (with a "liquidate all" recommendation).
/// </summary>
public sealed class DefaultRiskManager : IRiskManager
{
    private readonly OrchestrationOptions _options;

    /// <summary>
    /// 创建默认风控管理器。
    /// Creates the default risk manager.
    /// </summary>
    /// <param name="options">编排配置（不得为 null）/ Orchestration options (must not be null).</param>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when null.</exception>
    public DefaultRiskManager(OrchestrationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<RiskAssessment> AssessAsync(IReadOnlyList<TargetPosition> targets, PortfolioSnapshot current, CancellationToken ct)
    {
        if (targets == null)
        {
            throw new ArgumentNullException(nameof(targets));
        }

        if (current == null)
        {
            throw new ArgumentNullException(nameof(current));
        }

        ct.ThrowIfCancellationRequested();

        var reasons = new List<string>();

        // 规则 1：单标的权重上限 / Rule 1: per-symbol weight cap
        foreach (var target in targets)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.Symbol))
            {
                continue;
            }

            if (Math.Abs(target.TargetWeight) > _options.MaxWeightPerSymbol)
            {
                reasons.Add(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "symbol {0} weight {1:0.00} exceeds MaxWeightPerSymbol {2:0.00}",
                    target.Symbol, target.TargetWeight, _options.MaxWeightPerSymbol));
            }
        }

        // 规则 2：总敞口上限 / Rule 2: gross exposure cap
        var gross = targets.Where(t => t != null).Sum(t => Math.Abs(t.TargetWeight));
        if (gross > _options.MaxGrossExposure)
        {
            reasons.Add(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "gross exposure {0:0.00} exceeds MaxGrossExposure {1:0.00}", gross, _options.MaxGrossExposure));
        }

        // 规则 3：Kill-switch 回撤 / Rule 3: kill-switch drawdown (with liquidation recommendation)
        if (current.UnrealizedProfitRate <= _options.KillSwitchDrawdownRate)
        {
            reasons.Add(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "drawdown {0:0.00} at or below kill-switch {1:0.00}; recommend full liquidation of all positions",
                current.UnrealizedProfitRate, _options.KillSwitchDrawdownRate));
        }

        var assessment = new RiskAssessment
        {
            Approved = reasons.Count == 0
        };
        assessment.Reasons.AddRange(reasons);

        return Task.FromResult(assessment);
    }
}
