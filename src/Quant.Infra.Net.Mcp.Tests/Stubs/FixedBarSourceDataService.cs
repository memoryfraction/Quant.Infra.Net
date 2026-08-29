using Quant.Infra.Net.SourceData.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Mcp.Tests.Stubs;

/// <summary>
/// 假数据源（测试用）：返回指定数量的合成 K 线，零网络、确定性。
/// </summary>
internal sealed class FixedBarSourceDataService : Quant.Infra.Net.Mcp.DataSources.IMcpSourceDataService
{
    public FixedBarSourceDataService(int barCount, double basePrice = 100.0, string provider = "Fake")
    {
        Bars = barCount;
        BasePrice = basePrice;
        Provider = provider;
    }

    public string Provider { get; }
    public int Bars { get; }
    public double BasePrice { get; }

    public Task<Ohlcvs> DownloadDailyAsync(string symbol, DateTime start, DateTime end)
    {
        var ohlcvs = new Ohlcvs
        {
            Symbol = symbol.ToUpperInvariant(),
            StartDateTimeUtc = start,
            EndDateTimeUtc = end,
            OhlcvSet = new HashSet<Ohlcv>()
        };
        for (int i = 0; i < Bars; i++)
        {
            var t = start.AddDays(i);
            ohlcvs.OhlcvSet.Add(new Ohlcv
            {
                Symbol = symbol.ToUpperInvariant(),
                OpenDateTime = t,
                CloseDateTime = t.AddDays(1),
                Open = (decimal)(BasePrice + i * 0.01),
                High = (decimal)(BasePrice + i * 0.01 + 0.5),
                Low = (decimal)(BasePrice + i * 0.01 - 0.5),
                Close = (decimal)(BasePrice + i * 0.01 + 0.25),
                Volume = 1000000m,
                AdjustedClose = (decimal)(BasePrice + i * 0.01 + 0.25)
            });
        }
        return Task.FromResult(ohlcvs);
    }
}
