using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.Shared.Model;

namespace Quant.Infra.Net.Mcp.DataSources;

/// <summary>
/// MCP 数据源抽象（SOLID: Abstraction 依赖倒置）。
/// MCP data source abstraction (SOLID: Dependency Inversion).
/// </summary>
/// <remarks>
/// 这个接口是 MCP 层对"能下载 OHLCV"的最小抽象，不暴露具体提供商。
/// 用户通过 <see cref="IDataSourceFactory"/> + appsettings.json 决定用哪个实现（Finnhub / FMP / TwelveData）。
/// 放弃 Stooq：已被测试证明不稳定，不在 MCP 范围内。
/// </remarks>
public interface IMcpSourceDataService
{
    /// <summary>
    /// 提供商名称（Finnhub / Fmp / TwelveData）/ Provider name.
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// 下载日线 OHLCV / Downloads daily OHLCV.
    /// </summary>
    Task<Ohlcvs> DownloadDailyAsync(string symbol, DateTime start, DateTime end);
}
