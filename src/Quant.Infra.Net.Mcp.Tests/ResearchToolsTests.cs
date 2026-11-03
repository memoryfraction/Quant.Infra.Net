using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Mcp.DataSources;
using Quant.Infra.Net.Mcp.Engine;
using Quant.Infra.Net.Mcp.Strategies;
using Quant.Infra.Net.Mcp.Tools;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Mcp.Tests;

/// <summary>
/// v1.6 研究工具测试：免费数据源解析、多因子、报告、网格、优化、滚动前推（全部离线）。
/// v1.6 research tool tests: free data source parsing, multi-factor, report, grid, optimize, walk-forward (all offline).
/// </summary>
[TestClass]
public sealed class ResearchToolsTests
{
    private static string _dir = "";

    /// <summary>报告 / 缓存写到临时目录 / Reports and cache go to a temp dir.</summary>
    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _dir = Path.Combine(Path.GetTempPath(), "qin-mcp-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("QUANT_INFRA_REPORTS_DIR", Path.Combine(_dir, "reports"));
        Environment.SetEnvironmentVariable("QUANT_INFRA_CACHE_DIR", Path.Combine(_dir, "cache"));

        // 5 个合成 ETF，约 6 年日线，不同漂移/波动 / five synthetic ETFs, ~6 years of daily bars, different drift/vol
        var spec = new (string Sym, double Drift, double Vol, int Seed)[]
        {
            ("AAA", 0.0006, 0.010, 1), ("BBB", 0.0002, 0.006, 2), ("CCC", -0.0001, 0.015, 3), ("DDD", 0.0004, 0.012, 4), ("EEE", 0.0001, 0.004, 5)
        };
        foreach (var (sym, drift, vol, seed) in spec)
        {
            var rng = new Random(seed);
            var lines = new List<string> { "date,open,high,low,close,volume" };
            var px = 100.0;
            for (var d = new DateTime(2015, 1, 1); d <= new DateTime(2020, 12, 31); d = d.AddDays(1))
            {
                if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                var regime = d.Year == 2018 ? -2.0 : 1.0; // 2018 是"熊市" / 2018 is a bear year
                var open = px;
                px *= 1 + drift * regime + vol * (rng.NextDouble() * 2 - 1) * 1.7;
                lines.Add($"{d:yyyy-MM-dd},{open:0.####},{Math.Max(open, px) * 1.003:0.####},{Math.Min(open, px) * 0.997:0.####},{px:0.####},1000000");
            }

            File.WriteAllLines(Path.Combine(_dir, sym + ".csv"), lines);
        }
    }

    /// <summary>清理 / Cleanup.</summary>
    [ClassCleanup]
    public static void Cleanup()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [TestMethod]
    public void Parse_FreeSources_AndAliases()
    {
        Assert.IsNull(McpSourceDataFactory.Parse(null));
        Assert.IsNull(McpSourceDataFactory.Parse("demo"));
        Assert.AreEqual(McpSourceDataFactory.Provider.Yahoo, McpSourceDataFactory.Parse("Yahoo Finance"));
        Assert.AreEqual(McpSourceDataFactory.Provider.Binance, McpSourceDataFactory.Parse("crypto"));
        Assert.AreEqual(McpSourceDataFactory.Provider.Tiingo, McpSourceDataFactory.Parse("TIINGO"));
        Assert.ThrowsException<ArgumentException>(() => McpSourceDataFactory.Parse("Stooq"));
        Assert.ThrowsException<ArgumentException>(() => McpSourceDataFactory.Parse("nope"));
        Assert.IsFalse(McpSourceDataFactory.RequiresKey(McpSourceDataFactory.Provider.Yahoo));
        Assert.IsTrue(McpSourceDataFactory.RequiresKey(McpSourceDataFactory.Provider.Tiingo));
    }

    [TestMethod]
    public void Binance_NormalizesSymbols()
    {
        Assert.AreEqual("BTCUSDT", BinanceSourceDataService.Normalize("BTC"));
        Assert.AreEqual("BTCUSDT", BinanceSourceDataService.Normalize("btc-usd"));
        Assert.AreEqual("ETHUSDT", BinanceSourceDataService.Normalize("ETH/USDT"));
        Assert.AreEqual("ETHBTC", BinanceSourceDataService.Normalize("ETHBTC"));
    }

    [TestMethod]
    public void Yahoo_Parse_AdjustsForDividends_AndSkipsNulls()
    {
        const string json = """
            {"chart":{"result":[{"meta":{"gmtoffset":-18000},"timestamp":[1704205800,1704292200,1704378600],
             "indicators":{"quote":[{"open":[100,null,104],"high":[101,null,105],"low":[99,null,103],"close":[100,null,104],"volume":[10,null,30]}],
             "adjclose":[{"adjclose":[98,null,104]}]}}],"error":null}}
            """;
        var o = YahooSourceDataService.Parse("spy", json);
        var bars = o.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList();
        Assert.AreEqual(2, bars.Count, "null bar skipped");
        Assert.AreEqual(new DateTime(2024, 1, 2), bars[0].OpenDateTime);
        Assert.AreEqual(98m, bars[0].Close, "close is dividend-adjusted");
        Assert.AreEqual(98m, Math.Round(bars[0].Open, 6), "open scaled by adjclose/close");
        Assert.AreEqual("SPY", bars[0].Symbol);
    }

    [TestMethod]
    public void Binance_Parse_Klines()
    {
        var target = new Ohlcvs { Symbol = "BTCUSDT", OhlcvSet = new HashSet<Ohlcv>() };
        var n = BinanceSourceDataService.ParseInto(target,
            """[[1704067200000,"42283.58","44184.10","42180.77","44179.55","27174.29",1704153599999,"0",1,"0","0","0"]]""", out var last);
        Assert.AreEqual(1, n);
        Assert.AreEqual(1704067200000L, last);
        Assert.AreEqual(44179.55m, target.OhlcvSet.Single().Close);
    }

    [TestMethod]
    public void Grid_ParsesListsRangesAndPipes()
    {
        var g = ResearchService.ParseGrid("SlowPeriod=50,100; FastPeriod=1..21:10", 100);
        Assert.AreEqual(6, g.Count);
        var f = ResearchService.ParseGrid("Factors=momentum=1|momentum=1,lowvol=1", 100);
        Assert.AreEqual("momentum=1,lowvol=1", f[1]["Factors"]);
        Assert.ThrowsException<ArgumentException>(() => ResearchService.ParseGrid("A=1..100", 10));
    }

    [TestMethod]
    public void MultiFactor_ScoresAndValidatesFactors()
    {
        var o = MultiFactorRotation.Options.From(k => k switch { "Universe" => "a,b,c,d,e,f", "Factors" => "momentum=1,lowvol=0.5", _ => null });
        Assert.AreEqual(6, o.Universe.Count);
        Assert.AreEqual(2, o.TopK, "auto TopK = universe/3");
        var up = Enumerable.Range(0, 300).Select(i => 100.0 + i).ToList();
        var f = MultiFactorRotation.ComputeFactors(up, o);
        Assert.IsTrue(f["momentum"] > 0);
        Assert.IsTrue(f["trend"] > 0);
        Assert.ThrowsException<ArgumentException>(() => MultiFactorRotation.Options.From(k => k == "Factors" ? "value=1" : null));
    }

    [TestMethod]
    public async Task RunBacktest_MultiFactor_LocalDirectory_WritesReport()
    {
        var json = await RunBacktestTool.RunBacktest(
            strategy: "MultiFactor", startDate: "2016-01-01", endDate: "2020-12-31",
            universe: "AAA,BBB,CCC,DDD", factors: "momentum=1,lowvol=0.5", topK: 2, defensiveSymbol: "EEE",
            regimeSymbol: "AAA", dataSource: "LocalFile", localFilePath: _dir);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.AreEqual("LocalFile", root.GetProperty("dataSource").GetString());
        Assert.AreEqual(5, root.GetProperty("symbols").GetArrayLength(), "universe + defensive");
        Assert.IsTrue(root.GetProperty("trades").GetInt32() > 10, "rotation trades");
        Assert.IsTrue(root.GetProperty("regimes").GetArrayLength() >= 2, "regime breakdown present");
        Assert.IsTrue(root.GetProperty("yearly").GetArrayLength() >= 5);
        var path = root.GetProperty("report").GetProperty("path").GetString()!;
        Assert.IsTrue(File.Exists(path));
        var html = File.ReadAllText(path);
        StringAssert.Contains(html, "<svg");
        StringAssert.Contains(html, "Market regimes");
    }

    [TestMethod]
    public async Task RunBacktest_WarmupIsFetched_SoFirstEquityPointIsAtStart()
    {
        var json = await RunBacktestTool.RunBacktest(
            strategy: "MaCross", startDate: "2017-01-01", endDate: "2019-12-31", symbol: "AAA", slowPeriod: 200,
            dataSource: "LocalFile", localFilePath: Path.Combine(_dir, "AAA.csv"), report: false);
        using var doc = JsonDocument.Parse(json);
        var curve = doc.RootElement.GetProperty("equityCurveMonthly");
        Assert.IsTrue(curve[0].GetProperty("date").GetString()!.StartsWith("2017-01"), "evaluation starts at startDate (warm-up excluded)");
        Assert.IsTrue(doc.RootElement.GetProperty("trades").GetInt32() > 0, "signals available from day one thanks to warm-up");
    }

    [TestMethod]
    public async Task OptimizeStrategy_ReturnsInAndOutOfSample()
    {
        var json = await OptimizeTools.OptimizeStrategy(
            strategy: "MaCross", startDate: "2016-01-01", endDate: "2020-12-31", grid: "SlowPeriod=50,100,200",
            symbol: "DDD", dataSource: "LocalFile", localFilePath: Path.Combine(_dir, "DDD.csv"));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.AreEqual(3, root.GetProperty("combinationsTested").GetInt32());
        Assert.IsTrue(root.GetProperty("mostRobust").GetProperty("outOfSample").TryGetProperty("sharpe", out _));
        Assert.IsTrue(root.GetProperty("verdict").GetArrayLength() >= 2);
    }

    [TestMethod]
    public async Task WalkForward_StitchesOutOfSampleWindows()
    {
        var json = await OptimizeTools.WalkForward(
            strategy: "MultiFactor", startDate: "2016-01-01", endDate: "2020-12-31", grid: "TopK=1,2",
            trainMonths: 24, testMonths: 12, universe: "AAA,BBB,CCC,DDD", dataSource: "LocalFile", localFilePath: _dir);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.AreEqual(3, root.GetProperty("windows").GetArrayLength(), "2016-17 train → 2018, 2019, 2020 tests");
        Assert.IsTrue(root.TryGetProperty("walkForwardEfficiency", out _));
        Assert.IsTrue(File.Exists(root.GetProperty("report").GetProperty("path").GetString()));
    }

    [TestMethod]
    public void ListDataSources_IsValidJson_AndListsFreeSources()
    {
        using var doc = JsonDocument.Parse(ListDataSourcesTool.ListDataSources());
        var names = doc.RootElement.GetProperty("sources").EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
        CollectionAssert.IsSubsetOf(new[] { "Yahoo", "Binance", "Tiingo", "LocalFile", "Demo" }, names);
    }

    [TestMethod]
    public void ListStrategies_IncludesMultiFactor()
    {
        using var doc = JsonDocument.Parse(ListStrategiesTool.ListStrategies().Result);
        Assert.IsTrue(doc.RootElement.GetProperty("strategies").EnumerateArray().Any(e => e.GetProperty("name").GetString() == "MultiFactor"));
    }

    [TestMethod]
    public void Cache_ReusesCoveringRange()
    {
        var calls = 0;
        var inner = new CountingSource(() => calls++);
        var cache = new CachingSourceDataService(inner, Path.Combine(_dir, "cache-test"));
        cache.DownloadDailyAsync("X", new DateTime(2020, 1, 1), new DateTime(2020, 12, 31)).Wait();
        var sub = cache.DownloadDailyAsync("X", new DateTime(2020, 3, 1), new DateTime(2020, 3, 31)).Result;
        Assert.AreEqual(1, calls, "sub-range served from cache");
        Assert.IsTrue(sub.OhlcvSet.All(b => b.OpenDateTime.Month == 3));
    }

    /// <summary>
    /// 真实数据冒烟测试（需联网，设 QUANT_INFRA_LIVE_TESTS=1 才运行）/ Live smoke test (network; runs only with QUANT_INFRA_LIVE_TESTS=1).
    /// </summary>
    [TestMethod, TestCategory("Live")]
    public async Task Live_Yahoo_And_Binance()
    {
        if (Environment.GetEnvironmentVariable("QUANT_INFRA_LIVE_TESTS") != "1") Assert.Inconclusive("set QUANT_INFRA_LIVE_TESTS=1 to run");
        var spy = await RunBacktestTool.RunBacktest(strategy: "MaCross", startDate: "2010-01-01", endDate: "2024-12-31", symbol: "SPY", slowPeriod: 200, dataSource: "Yahoo");
        StringAssert.Contains(spy, "\"dataSource\": \"Yahoo\"");
        var btc = await RunBacktestTool.RunBacktest(strategy: "MaCross", startDate: "2020-01-01", endDate: "2024-12-31", symbol: "BTC", slowPeriod: 100, dataSource: "Binance");
        StringAssert.Contains(btc, "\"dataSource\": \"Binance\"");
    }

    private sealed class CountingSource : IMcpSourceDataService
    {
        private readonly Action _onCall;
        public CountingSource(Action onCall) => _onCall = onCall;
        public string Provider => "Counting";

        public Task<Ohlcvs> DownloadDailyAsync(string symbol, DateTime start, DateTime end)
        {
            _onCall();
            var set = new HashSet<Ohlcv>();
            for (var d = start; d <= end; d = d.AddDays(1))
                set.Add(new Ohlcv { Symbol = symbol, OpenDateTime = d, CloseDateTime = d.AddDays(1), Open = 1, High = 1, Low = 1, Close = 1, AdjustedClose = 1 });
            return Task.FromResult(new Ohlcvs { Symbol = symbol, OhlcvSet = set });
        }
    }
}
