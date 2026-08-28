using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.State;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// InMemoryPortfolioStateStore 单元测试。
/// InMemoryPortfolioStateStore unit tests.
/// </summary>
[TestClass]
public class PortfolioStateStoreTests
{
    private static PortfolioSnapshot NewSnapshot(decimal equity = 10000m)
        => new()
        {
            SnapshotUtc = DateTime.UtcNow,
            AccountEquityUsd = equity,
            ActualWeights = new Dictionary<string, double> { ["AAPL"] = 0.3 },
            TargetWeights = new Dictionary<string, double> { ["AAPL"] = 0.3 },
            UnrealizedProfitRate = 0.0d
        };

    /// <summary>
    /// Save 后 GetLatest 返回同一快照。
    /// After Save, GetLatest returns the same snapshot.
    /// </summary>
    [TestMethod]
    public async Task SaveThenGet_ReturnsSnapshot()
    {
        var store = new InMemoryPortfolioStateStore();
        var snapshot = NewSnapshot();
        await store.SaveSnapshotAsync(snapshot, CancellationToken.None);

        var latest = await store.GetLatestAsync(CancellationToken.None);
        Assert.AreSame(snapshot, latest);
    }

    /// <summary>
    /// 无数据时 GetLatest 返回 null。
    /// With no data, GetLatest returns null.
    /// </summary>
    [TestMethod]
    public async Task GetLatest_Empty_ReturnsNull()
    {
        var store = new InMemoryPortfolioStateStore();
        Assert.IsNull(await store.GetLatestAsync(CancellationToken.None));
    }

    /// <summary>
    /// 覆盖式保存：后写胜出。
    /// Overwrite semantics: the later save wins.
    /// </summary>
    [TestMethod]
    public async Task SaveTwice_LaterWins()
    {
        var store = new InMemoryPortfolioStateStore();
        await store.SaveSnapshotAsync(NewSnapshot(10000m), CancellationToken.None);
        var second = NewSnapshot(12000m);
        await store.SaveSnapshotAsync(second, CancellationToken.None);

        var latest = await store.GetLatestAsync(CancellationToken.None);
        Assert.AreEqual(12000m, latest!.AccountEquityUsd);
    }

    /// <summary>
    /// null 快照 → ArgumentNullException。
    /// Null snapshot → ArgumentNullException.
    /// </summary>
    [TestMethod]
    public void SaveNull_Throws()
    {
        var store = new InMemoryPortfolioStateStore();
        Assert.ThrowsException<ArgumentNullException>(() => store.SaveSnapshotAsync(null!, CancellationToken.None).GetAwaiter().GetResult());
    }

    /// <summary>
    /// 已取消令牌 → OperationCanceledException。
    /// A canceled token → OperationCanceledException.
    /// </summary>
    [TestMethod]
    public async Task AlreadyCanceledToken_Throws()
    {
        var store = new InMemoryPortfolioStateStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => store.GetLatestAsync(cts.Token));
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => store.SaveSnapshotAsync(NewSnapshot(), cts.Token));
    }
}
