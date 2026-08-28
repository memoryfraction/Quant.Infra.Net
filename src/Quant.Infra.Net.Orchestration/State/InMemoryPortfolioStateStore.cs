using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.State;

/// <summary>
/// 内存组合状态存储：仅保留最新一份快照（覆盖式）。
/// In-memory portfolio state store: keeps only the latest snapshot (overwrite).
/// </summary>
public sealed class InMemoryPortfolioStateStore : IPortfolioStateStore
{
    private readonly object _gate = new();
    private PortfolioSnapshot? _latest;

    /// <inheritdoc />
    public Task SaveSnapshotAsync(PortfolioSnapshot snapshot, CancellationToken ct)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _latest = snapshot;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<PortfolioSnapshot?> GetLatestAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_latest);
        }
    }
}
