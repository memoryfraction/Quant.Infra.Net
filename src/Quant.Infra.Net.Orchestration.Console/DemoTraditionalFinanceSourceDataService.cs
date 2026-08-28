using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration.Console;

/// <summary>
/// 演示宿主专用行情源：按标的生成确定性离线 K 线（零网络访问）。
/// Demo-host-only market data source: deterministic offline candles per symbol (zero network access).
/// </summary>
/// <remarks>
/// 序列形状针对三种演示策略设计：AAPL=稳定上升趋势（MaCross/MeanReversion 可出信号），
/// AAA+BBB=高相关配对且末点显著偏离（PairTradingZScore 可出方向信号）。
/// Shapes are crafted for the three demo strategies: AAPL = steady uptrend; AAA+BBB = highly correlated pair with a terminal deviation.
/// </remarks>
public sealed class DemoTraditionalFinanceSourceDataService : ITraditionalFinanceSourceDataService
{
    private const int PairBars = 150;
    private const int SoloBars = 260;

    /// <summary>
    /// 同步每日数据（演示宿主不使用）/ demo host does not use daily sync.
    /// </summary>
    public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, ResolutionLevel Period = ResolutionLevel.Daily)
        => Task.FromResult(OhlcvsFor(symbol, startDt, ResolutionLevel.Daily));

    /// <summary>
    /// 下载 OHLCV：按标的返回确定性序列。/ Download: deterministic per-symbol series.
    /// </summary>
    public Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel Period = ResolutionLevel.Daily, DataSource dataSource = DataSource.YahooFinance)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("symbol must not be blank.", nameof(symbol));
        }

        return Task.FromResult(OhlcvsFor(symbol, startDt, Period));
    }

    /// <summary>
    /// 从文件读取（演示宿主不使用）/ unused by the demo host.
    /// </summary>
    public Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename) => Task.FromResult(new List<Ohlcv>());

    /// <summary>
    /// 保存 OHLCV（演示宿主不使用）/ unused by the demo host.
    /// </summary>
    public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName) => Task.CompletedTask;

    /// <summary>
    /// SP500 列表（演示宿主不使用）/ unused by the demo host.
    /// </summary>
    public Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500) => Task.FromResult(Enumerable.Empty<string>());

    private static Ohlcvs OhlcvsFor(string symbol, DateTime startDt, ResolutionLevel period)
    {
        var step = period switch
        {
            ResolutionLevel.Hourly => TimeSpan.FromHours(1),
            ResolutionLevel.Weekly => TimeSpan.FromDays(7),
            ResolutionLevel.Monthly => TimeSpan.FromDays(30),
            _ => TimeSpan.FromDays(1)
        };

        var closes = ClosesFor(symbol, period);
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

    private static double[] ClosesFor(string symbol, ResolutionLevel period)
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
                // 稳定上升趋势 + 轻微周期波动：MaCross(last > SMA200) 与 MeanReversion(z≥+EntryZ → Short) 均可触发
                // （末 100 根窗口 z≈2.5，超过默认 EntryZ 2.0）。
                // Steady uptrend + mild wobble: triggers MaCross(last > SMA200) and MeanReversion(z≈+2.5 ≥ EntryZ → Short).
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
        // BBB = 100 + AR(0.4) 平稳游走；AAA = 1.2*BBB + AR(0.4)，AAA 末点 +5 显著正偏离 → 配对 z 显著 → 方向信号。
        // BBB = 100 + stationary AR(0.4); AAA = 1.2*BBB + AR(0.4); AAA's last bar deviates +5 → strong pair z → directional signal.
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

        legA[n - 1] += 5.0; // 末点偏离（配对最后一根显著失衡）/ terminal deviation (final bar strongly out of balance)

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
