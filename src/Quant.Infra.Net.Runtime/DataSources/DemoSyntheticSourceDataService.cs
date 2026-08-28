using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Runtime.DataSources;

/// <summary>
/// 离线合成行情源（Demo 数据种类）：按标的生成确定性 K 线序列，零网络访问（设计 §5.3/§7.7）。
/// 序列形状与 Orchestration 演示宿主同源（AAPL=稳定上升趋势；AAA/BBB=高相关配对且末点显著偏离），
/// 保证同一参数下策略信号跨宿主一致。
/// Offline synthetic data source (Demo kind): deterministic per-symbol candles with zero network access.
/// Series shapes mirror the orchestration demo host (AAPL = steady uptrend; AAA/BBB = correlated pair with a
/// terminal deviation) so strategy signals stay consistent across hosts for the same parameters.
/// </summary>
public sealed class DemoSyntheticSourceDataService : ITraditionalFinanceSourceDataService
{
    private const int PairBars = 150;
    private const int SoloBars = 260;

    /// <summary>
    /// 同步每日数据（演示宿主不使用）。
    /// Begin syncing daily data (unused by demo hosts).
    /// </summary>
    public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, Shared.Model.ResolutionLevel Period = Shared.Model.ResolutionLevel.Daily)
        => Task.FromResult(OhlcvsFor(symbol, startDt, Period));

    /// <summary>
    /// 下载 OHLCV：按标的返回确定性序列（忽略 dataSource 分支）。
    /// Download OHLCV: returns the deterministic per-symbol series (ignores the dataSource branch).
    /// </summary>
    public Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, Shared.Model.ResolutionLevel Period = Shared.Model.ResolutionLevel.Daily, Shared.Model.DataSource dataSource = Shared.Model.DataSource.YahooFinance)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("symbol must not be blank.", nameof(symbol));
        }

        return Task.FromResult(OhlcvsFor(symbol, startDt, Period));
    }

    /// <summary>
    /// 从文件读取（演示宿主不使用）/ Read from file (unused by demo hosts).
    /// </summary>
    public Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename) => Task.FromResult(new List<Ohlcv>());

    /// <summary>
    /// 保存 OHLCV（演示宿主不使用）/ Save OHLCV (unused by demo hosts).
    /// </summary>
    public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName) => Task.CompletedTask;

    /// <summary>
    /// SP500 列表（演示宿主不使用）/ S&P 500 list (unused by demo hosts).
    /// </summary>
    public Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500) => Task.FromResult(Enumerable.Empty<string>());

    private static Ohlcvs OhlcvsFor(string symbol, DateTime startDt, Shared.Model.ResolutionLevel period)
    {
        var step = period switch
        {
            Shared.Model.ResolutionLevel.Hourly => TimeSpan.FromHours(1),
            Shared.Model.ResolutionLevel.Weekly => TimeSpan.FromDays(7),
            Shared.Model.ResolutionLevel.Monthly => TimeSpan.FromDays(30),
            _ => TimeSpan.FromDays(1)
        };

        var closes = ClosesFor(symbol);
        var set = new HashSet<Ohlcv>(closes.Length);
        for (var i = 0; i < closes.Length; i++)
        {
            var dt = startDt.Add(step * i);
            var price = (decimal)Math.Round(closes[i], 4);
            set.Add(new Ohlcv
            {
                Symbol = symbol,
                OpenDateTime = dt,
                CloseDateTime = dt,
                Open = price,
                High = price,
                Low = price,
                Close = price,
                Volume = 1m
            });
        }

        return new Ohlcvs { Symbol = symbol, OhlcvSet = set };
    }

    private static double[] ClosesFor(string symbol)
    {
        var n = symbol is ("AAA" or "BBB") ? PairBars : SoloBars;
        var seed = 17;
        foreach (var c in symbol)
        {
            seed = seed * 31 + c;
        }

        var rng = new Random(seed & 0x7fffffff);

        switch (symbol.ToUpperInvariant())
        {
            case "AAA" or "BBB":
                return PairLeg(n, seedB: symbol == "BBB");

            default:
                // 稳定上升趋势 + 轻微周期波动（与 Orchestration 演示宿主一致）。
                // Steady uptrend + mild wobble (identical to the orchestration demo host).
                var trend = new double[n];
                for (var i = 0; i < n; i++)
                {
                    trend[i] = 100.0 * Math.Pow(1.005, i) + Math.Sin(i / 6.28) + (rng.NextDouble() - 0.5) * 0.4;
                }

                return trend;
        }
    }

    private static double[] PairLeg(int n, bool seedB)
    {
        // BBB = 100 + AR(0.4) 平稳游走；AAA = 1.2*BBB + AR(0.4)，AAA 末点 +5 显著正偏离 → 配对 z 显著。
        // BBB = 100 + stationary AR(0.4); AAA = 1.2*BBB + AR(0.4); AAA's last bar deviates +5.
        var rngA = new Random(seedFrom("AAA"));
        var rngB = new Random(seedFrom("BBB"));

        var legA = new double[n];
        var legB = new double[n];
        var noiseA = 0d;
        var noiseB = 0d;
        for (var i = 0; i < n; i++)
        {
            noiseA = 0.4 * noiseA + (rngA.NextDouble() - 0.5) * 0.6;
            noiseB = 0.4 * noiseB + (rngB.NextDouble() - 0.5) * 2.0;
            legB[i] = 100.0 + noiseB;
            legA[i] = 1.2 * legB[i] + noiseA;
        }

        legA[n - 1] += 5.0; // 末点偏离（配对最后一根显著失衡）/ terminal deviation

        return seedB ? legB : legA;

        static int seedFrom(string s)
        {
            var v = 23;
            foreach (var c in s)
            {
                v = v * 31 + c;
            }

            return v & 0x7fffffff;
        }
    }
}
