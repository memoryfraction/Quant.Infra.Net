using System.IO;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Mcp.DataSources;
using Quant.Infra.Net.Mcp.Tools;

namespace Quant.Infra.Net.Mcp.Tests;

/// <summary>
/// LocalFile 数据源测试：从本地 CSV / JSON 文件读取日线 OHLCV，零网络、确定性。
/// </summary>
[TestClass]
public class LocalFileSourceTests
{
    [TestCleanup]
    public void Cleanup()
    {
        foreach (var f in new[] { "_test_aapl.csv", "_test_aapl.json" })
        {
            var p = Path.Combine(AppContext.BaseDirectory, f);
            if (File.Exists(p)) File.Delete(p);
        }
    }

    [TestMethod]
    public async Task FetchOhlcv_LocalCsv_ReturnsBars()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "_test_aapl.csv");
        File.WriteAllText(csvPath, "date,open,high,low,close,volume\n" +
            "2024-01-02,185.6,187.2,184.1,186.5,5000000\n" +
            "2024-01-03,186.5,188.9,185.0,188.2,6000000\n" +
            "2024-01-04,188.2,189.5,187.3,189.0,5500000\n");

        var json = await FetchOhlcvTool.FetchOhlcv(
            symbol: "AAPL",
            startDate: "2024-01-01",
            endDate: "2024-01-31",
            dataSource: "LocalFile",
            localFilePath: csvPath);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.AreEqual("LocalFile", root.GetProperty("provider").GetString());
        var bars = root.GetProperty("bars");
        Assert.AreEqual(3, bars.GetArrayLength());
        Assert.AreEqual(185.6m, bars[0].GetProperty("open").GetDecimal());
        Assert.AreEqual(189.0m, bars[2].GetProperty("close").GetDecimal());
        Assert.IsFalse(root.GetProperty("truncated").GetBoolean());
    }

    [TestMethod]
    public async Task FetchOhlcv_LocalJson_ReturnsBars()
    {
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "_test_aapl.json");
        File.WriteAllText(jsonPath, "[\n" +
            "  {\"date\":\"2024-01-02\",\"open\":185.6,\"high\":187.2,\"low\":184.1,\"close\":186.5,\"volume\":5000000},\n" +
            "  {\"date\":\"2024-01-03\",\"open\":186.5,\"high\":188.9,\"low\":185.0,\"close\":188.2,\"volume\":6000000}\n" +
            "]");

        var json = await FetchOhlcvTool.FetchOhlcv(
            symbol: "AAPL",
            startDate: "2024-01-01",
            endDate: "2024-01-31",
            dataSource: "LocalFile",
            localFilePath: jsonPath);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.AreEqual("LocalFile", root.GetProperty("provider").GetString());
        Assert.AreEqual(2, root.GetProperty("bars").GetArrayLength());
    }

    [TestMethod]
    public void LocalFile_MissingFile_Throws()
    {
        try
        {
            var svc = new LocalFileSourceDataService("/nonexistent/file.csv");
            Assert.Fail("expected FileNotFoundException");
        }
        catch (FileNotFoundException)
        {
            // expected
        }
    }
}
