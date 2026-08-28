namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 风控评估结果：是否放行 + 全部拒绝理由（多条规则同时触发时全部列出）。
/// Risk assessment result: whether to approve plus every rejection reason (all triggered rules are listed).
/// </summary>
public class RiskAssessment
{
    /// <summary>
    /// 是否通过全部风控规则。
    /// Whether all risk rules passed.
    /// </summary>
    public bool Approved { get; init; }

    /// <summary>
    /// 拒绝/触发理由列表（通过时为空）。
    /// Rejection/trigger reasons (empty when approved).
    /// </summary>
    public List<string> Reasons { get; } = new(8);
}
