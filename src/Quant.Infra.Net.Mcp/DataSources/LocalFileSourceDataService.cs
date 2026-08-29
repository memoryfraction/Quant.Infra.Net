using System.Globalization;
using System.IO;
using Quant.Infra.Net.SourceData.Model;
using System.Text.Json;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// LocalFile 数据源：从本地 CSV / JSON 文件读取日线 OHLCV。
/// 零网络、零依赖、确定性 —— 最适合做"范例稳定运行"的稳定数据源。
/// </summary>
/// <remarks>
/// 支持两种格式（按文件扩展名自动选择）：
///   .csv  → 表头: date,open,high,low,close,volume[,adjusted_close]
///   .json → [ { "date": "2024-01-02", "open": 185.6, ... }, ... ]
/// 文件路径通过 <see cref="File"/> 指定（绝对路径或相对于 AppContext.BaseDirectory）。
/// </remarks>
public sealed class LocalFileSourceDataService : IMcpSourceDataService
{
    private readonly string _filePath;

    public LocalFileSourceDataService(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath is required.", nameof(filePath));
        _filePath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(AppContext.BaseDirectory, filePath);
        if (!File.Exists(_filePath))
            throw new FileNotFoundException($"LocalFile data source: file not found: {_filePath}");
    }

    public string Provider => "LocalFile";

    public Task<Ohlcvs> DownloadDailyAsync(string symbol, DateTime start, DateTime end)
    {
        var ext = Path.GetExtension(_filePath).ToLowerInvariant();
        var bars = ext == ".json" ? ReadJson() : ReadCsv();

        // 过滤窗口（按 OpenDateTime 日期）。
        var filtered = bars
            .Where(b => b.OpenDateTime.Date >= start.Date && b.OpenDateTime.Date <= end.Date)
            .OrderBy(b => b.OpenDateTime)
            .ToList();

        var ohlcvs = new Ohlcvs
        {
            Symbol = symbol.ToUpperInvariant(),
            StartDateTimeUtc = filtered.Count > 0 ? filtered[0].OpenDateTime : start,
            EndDateTimeUtc = filtered.Count > 0 ? filtered[^1].OpenDateTime : end,
            OhlcvSet = new HashSet<Ohlcv>(filtered)
        };
        return Task.FromResult(ohlcvs);
    }

    private List<Ohlcv> ReadCsv()
    {
        var lines = File.ReadAllLines(_filePath);
        if (lines.Length == 0) return new List<Ohlcv>();
        var header = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();
        int iDate = Array.IndexOf(header, "date");
        int iOpen = Array.IndexOf(header, "open");
        int iHigh = Array.IndexOf(header, "high");
        int iLow = Array.IndexOf(header, "low");
        int iClose = Array.IndexOf(header, "close");
        int iVol = Array.IndexOf(header, "volume");
        int iAdj = Array.IndexOf(header, "adjusted_close");

        var bars = new List<Ohlcv>();
        for (int i = 1; i < lines.Length; i++)
        {
            var cells = lines[i].Split(',');
            if (cells.Length < 6) continue;
            var dt = DateTime.Parse(cells[iDate].Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            bars.Add(new Ohlcv
            {
                Symbol = "",
                OpenDateTime = dt,
                CloseDateTime = dt.AddDays(1),
                Open = decimal.Parse(cells[iOpen].Trim(), CultureInfo.InvariantCulture),
                High = decimal.Parse(cells[iHigh].Trim(), CultureInfo.InvariantCulture),
                Low = decimal.Parse(cells[iLow].Trim(), CultureInfo.InvariantCulture),
                Close = decimal.Parse(cells[iClose].Trim(), CultureInfo.InvariantCulture),
                Volume = iVol >= 0 ? decimal.Parse(cells[iVol].Trim(), CultureInfo.InvariantCulture) : 0m,
                AdjustedClose = iAdj >= 0 ? decimal.Parse(cells[iAdj].Trim(), CultureInfo.InvariantCulture)
                                          : decimal.Parse(cells[iClose].Trim(), CultureInfo.InvariantCulture)
            });
        }
        return bars;
    }

    private List<Ohlcv> ReadJson()
    {
        var json = File.ReadAllText(_filePath);
        using var doc = JsonDocument.Parse(json);
        var bars = new List<Ohlcv>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var dt = DateTime.Parse(item.GetProperty("date").GetString()!,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            bars.Add(new Ohlcv
            {
                Symbol = "",
                OpenDateTime = dt,
                CloseDateTime = dt.AddDays(1),
                Open = item.GetProperty("open").GetDecimal(),
                High = item.GetProperty("high").GetDecimal(),
                Low = item.GetProperty("low").GetDecimal(),
                Close = item.GetProperty("close").GetDecimal(),
                Volume = item.TryGetProperty("volume", out var v) ? v.GetDecimal() : 0m,
                AdjustedClose = item.TryGetProperty("adjusted_close", out var ac) ? ac.GetDecimal()
                                        : item.GetProperty("close").GetDecimal()
            });
        }
        return bars;
    }
}
