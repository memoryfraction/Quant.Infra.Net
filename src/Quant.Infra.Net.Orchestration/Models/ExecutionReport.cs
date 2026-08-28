namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 单次执行报告：调仓前后权重、成功标志与错误信息。
/// Per-symbol execution report: weights before/after, success flag, and error message.
/// </summary>
public class ExecutionReport
{
    /// <summary>
    /// 标的代码。
    /// Trading symbol.
    /// </summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// 调仓前实际权重。
    /// Actual weight before rebalancing.
    /// </summary>
    public double PreviousWeight { get; init; }

    /// <summary>
    /// 调仓后实际权重。
    /// Actual weight after rebalancing.
    /// </summary>
    public double CurrentWeight { get; init; }

    /// <summary>
    /// 是否成功（死区内跳过也算成功）。
    /// Whether it succeeded (a skip inside the dead zone also counts as success).
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 错误信息（成功时为 null）。
    /// Error message (null on success).
    /// </summary>
    public string? ErrorMessage { get; init; }
}
