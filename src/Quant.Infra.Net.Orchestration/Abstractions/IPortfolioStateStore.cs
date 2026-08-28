using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Abstractions;

/// <summary>
/// 组合状态存储抽象：保存与读取最新组合快照。
/// Portfolio state store abstraction: persists and retrieves the latest portfolio snapshot.
/// </summary>
public interface IPortfolioStateStore
{
    /// <summary>
    /// 保存组合快照（覆盖最新值）。
    /// Saves a portfolio snapshot (replacing the latest value).
    /// </summary>
    /// <param name="snapshot">快照（不得为 null）/ Snapshot (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>表示保存完成的任务 / Task representing the completed save.</returns>
    Task SaveSnapshotAsync(PortfolioSnapshot snapshot, CancellationToken ct);

    /// <summary>
    /// 读取最新快照；无数据返回 null。
    /// Retrieves the latest snapshot; returns null when no data exists.
    /// </summary>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>最新快照（无数据为 null）/ Latest snapshot (null when absent).</returns>
    Task<PortfolioSnapshot?> GetLatestAsync(CancellationToken ct);
}
