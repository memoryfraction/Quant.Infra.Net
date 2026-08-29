using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Backtest.Tests;

/// <summary>
/// B1：<see cref="HistoricalDataSet"/> 行为（排序去重 / Timeline 并集 / SliceUpTo 边界 / CloseAt）。
/// B1: HistoricalDataSet behavior (sorting/unique timeline, SliceUpTo boundaries, CloseAt).
/// </summary>
[TestClass]
public sealed class HistoricalDataSetTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void Constructor_SortsAndDedupes_UnorderedInput_BecomesAscendingTimeline()
    {
        var t1 = T0.AddDays(1);
        var t2 = T0.AddDays(2);

        // 乱序 + 一根完全重复的 K 线 / shuffled input + one exact duplicate bar
        var series = new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            ["AAA"] = new List<Ohlcv>
            {
                TestBars.Bar("AAA", t2, 30m),
                TestBars.Bar("AAA", T0, 10m),
                TestBars.Bar("AAA", t1, 20m),
                TestBars.Bar("AAA", t1, 20m),
            },
        };

        var set = new HistoricalDataSet(series);

        Assert.AreEqual(3, set.Timeline.Count);
        Assert.AreEqual(T0, set.Timeline[0]);
        Assert.AreEqual(t1, set.Timeline[1]);
        Assert.AreEqual(t2, set.Timeline[2]);

        var slice = set.SliceUpTo("AAA", t1);
        Assert.AreEqual(2, slice.OhlcvSet.Count);
        var ordered = slice.OhlcvSet.OrderBy(b => b.OpenDateTime).Select(b => b.Close).ToList();
        CollectionAssert.AreEqual(new decimal[] { 10m, 20m }, ordered);
    }

    [TestMethod]
    public void SliceUpTo_Boundary_IncludesBarAtAsOf_ExcludesBarAfterIt()
    {
        var t1 = T0.AddDays(1);
        var t2 = T0.AddDays(2);
        var series = new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            ["AAA"] = new List<Ohlcv>
            {
                TestBars.Bar("AAA", T0, 10m),
                TestBars.Bar("AAA", t1, 20m),
                TestBars.Bar("AAA", t2, 30m),
            },
        };

        var set = new HistoricalDataSet(series);

        // 恰好等于 asOfUtc 的 bar 含入 / exactly-at-asOfUtc bar is included
        Assert.AreEqual(3, set.SliceUpTo("AAA", t2).OhlcvSet.Count);

        // 之后一根不含入 / the bar after asOfUtc is excluded
        var boundary = set.SliceUpTo("AAA", t1).OhlcvSet;
        CollectionAssert.AreEqual(
            new[] { T0, t1 }.OrderBy(x => x).ToList(),
            boundary.Select(b => b.OpenDateTime).OrderBy(x => x).ToList());

        // 首个 bar 之前：空切片 / before the first bar: empty slice
        Assert.AreEqual(0, set.SliceUpTo("AAA", T0 - TimeSpan.FromTicks(1)).OhlcvSet.Count);
    }

    [TestMethod]
    public void Timeline_UnalignedSymbols_ReturnsAscendingUnion()
    {
        var t1 = T0.AddHours(6);
        var t2 = T0.AddDays(1);
        var series = new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            ["AAA"] = new List<Ohlcv> { TestBars.Bar("AAA", T0, 10m), TestBars.Bar("AAA", t2, 30m) },
            ["BBB"] = new List<Ohlcv> { TestBars.Bar("BBB", t1, 40m), TestBars.Bar("BBB", t2, 50m) },
        };

        var set = new HistoricalDataSet(series);

        CollectionAssert.AreEqual(new[] { T0, t1, t2 }, set.Timeline.ToArray());
    }

    [TestMethod]
    public void CloseAt_ReturnsCloseOfLastBarAtOrBeforeAsOf_NullWhenAbsent()
    {
        var t1 = T0.AddDays(1);
        var series = new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            ["AAA"] = new List<Ohlcv> { TestBars.Bar("AAA", T0, 10m), TestBars.Bar("AAA", t1, 20m) },
        };

        var set = new HistoricalDataSet(series);

        Assert.AreEqual(20d, set.CloseAt("AAA", t1));
        Assert.AreEqual(10d, set.CloseAt("AAA", T0));
        Assert.AreEqual(null, set.CloseAt("AAA", T0 - TimeSpan.FromTicks(1)));
        Assert.AreEqual(null, set.CloseAt("ZZZ", t1));
    }

    [TestMethod]
    public void SliceUpTo_UnknownSymbol_ReturnsEmptySet_WithoutThrowing()
    {
        var set = new HistoricalDataSet(
            new Dictionary<string, IReadOnlyList<Ohlcv>>
            {
                ["AAA"] = new List<Ohlcv> { TestBars.Bar("AAA", T0, 10m) },
            });

        var slice = set.SliceUpTo("NOPE", T0);

        Assert.AreEqual(0, slice.OhlcvSet.Count);
        Assert.IsTrue(string.Equals(slice.Symbol, "NOPE", StringComparison.OrdinalIgnoreCase));
    }
}
