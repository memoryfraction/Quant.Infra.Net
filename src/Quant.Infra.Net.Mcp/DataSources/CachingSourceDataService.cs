using System.Text.Json;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// 磁盘缓存装饰器：同一 provider + symbol 的已下载区间直接复用，避免限流，让"自然语言反复调参"秒级返回。
/// Disk-cache decorator: reuses already-downloaded ranges per provider + symbol, avoiding rate limits so iterative
/// natural-language optimization returns in seconds.
/// </summary>
/// <remarks>
/// 位置：环境变量 <c>QUANT_INFRA_CACHE_DIR</c>，否则 <c>%LOCALAPPDATA%/QuantInfraNet/cache</c>。
/// 新鲜度：区间终点早于 3 天前的数据视为永久有效（历史不变）；否则 12 小时过期。设 <c>QUANT_INFRA_CACHE=off</c> 关闭。
/// Location: env <c>QUANT_INFRA_CACHE_DIR</c>, else <c>%LOCALAPPDATA%/QuantInfraNet/cache</c>.
/// Freshness: ranges ending more than 3 days ago never expire (history doesn't change); otherwise 12 hours.
/// Set <c>QUANT_INFRA_CACHE=off</c> to disable.
/// </remarks>
public sealed class CachingSourceDataService : IMcpSourceDataService
{
    private readonly IMcpSourceDataService _inner;
    private readonly string _root;

    /// <summary>包装一个数据源 / Wraps a data source.</summary>
    public CachingSourceDataService(IMcpSourceDataService inner, string? cacheRoot = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _root = cacheRoot ?? DefaultRoot();
    }

    /// <inheritdoc />
    public string Provider => _inner.Provider;

    /// <summary>缓存是否全局启用 / Whether caching is enabled globally.</summary>
    public static bool Enabled
        => !string.Equals(Environment.GetEnvironmentVariable("QUANT_INFRA_CACHE"), "off", StringComparison.OrdinalIgnoreCase);

    /// <summary>默认缓存根目录 / Default cache root.</summary>
    public static string DefaultRoot()
    {
        var env = Environment.GetEnvironmentVariable("QUANT_INFRA_CACHE_DIR");
        return !string.IsNullOrWhiteSpace(env)
            ? env
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuantInfraNet", "cache");
    }

    /// <inheritdoc />
    public async Task<Ohlcvs> DownloadDailyAsync(string symbol, DateTime start, DateTime end)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var dir = Path.Combine(_root, Safe(_inner.Provider), Safe(sym));
        var hit = TryRead(dir, start.Date, end.Date);
        if (hit is not null)
        {
            return Filter(sym, hit, start, end);
        }

        var fresh = await _inner.DownloadDailyAsync(symbol, start, end).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"{start:yyyyMMdd}_{end:yyyyMMdd}.json");
            var rows = fresh.OhlcvSet.OrderBy(b => b.OpenDateTime).Select(b => new Row(
                b.OpenDateTime, (double)b.Open, (double)b.High, (double)b.Low, (double)b.Close, (double)b.Volume, (double)b.AdjustedClose)).ToList();
            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(rows)).ConfigureAwait(false);
        }
        catch
        {
            // 缓存失败不影响主流程 / A cache failure never breaks the request.
        }

        return Filter(sym, fresh.OhlcvSet.ToList(), start, end);
    }

    private static List<Ohlcv>? TryRead(string dir, DateTime start, DateTime end)
    {
        if (!Directory.Exists(dir)) return null;
        var staleCutoff = DateTime.UtcNow.Date.AddDays(-3);
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file).Split('_');
            if (name.Length != 2 ||
                !DateTime.TryParseExact(name[0], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fs) ||
                !DateTime.TryParseExact(name[1], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fe))
            {
                continue;
            }

            if (fs > start || fe < end) continue;
            var fresh = fe < staleCutoff || File.GetLastWriteTimeUtc(file) > DateTime.UtcNow.AddHours(-12);
            if (!fresh) continue;

            try
            {
                var rows = JsonSerializer.Deserialize<List<Row>>(File.ReadAllText(file));
                if (rows is null) continue;
                return rows.Select(r => new Ohlcv
                {
                    OpenDateTime = DateTime.SpecifyKind(r.T, DateTimeKind.Utc),
                    CloseDateTime = DateTime.SpecifyKind(r.T.AddDays(1), DateTimeKind.Utc),
                    Open = (decimal)r.O,
                    High = (decimal)r.H,
                    Low = (decimal)r.L,
                    Close = (decimal)r.C,
                    Volume = (decimal)r.V,
                    AdjustedClose = (decimal)r.A
                }).ToList();
            }
            catch
            {
                // 损坏文件忽略 / ignore corrupt files
            }
        }

        return null;
    }

    private static Ohlcvs Filter(string sym, IEnumerable<Ohlcv> bars, DateTime start, DateTime end)
    {
        var set = new HashSet<Ohlcv>();
        foreach (var b in bars.Where(b => b.OpenDateTime.Date >= start.Date && b.OpenDateTime.Date <= end.Date))
        {
            b.Symbol = sym;
            set.Add(b);
        }

        return new Ohlcvs
        {
            Symbol = sym,
            OhlcvSet = set,
            StartDateTimeUtc = set.Count > 0 ? set.Min(b => b.OpenDateTime) : start,
            EndDateTimeUtc = set.Count > 0 ? set.Max(b => b.CloseDateTime) : end
        };
    }

    private static string Safe(string s)
        => string.Concat(s.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == '^' || ch == '=' ? '_' : ch));

    private sealed record Row(DateTime T, double O, double H, double L, double C, double V, double A);
}
