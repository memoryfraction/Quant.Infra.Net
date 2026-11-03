using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Quant.Infra.Net.Mcp.Tools;

/// <summary>
/// MCP 工具类型：list_data_sources —— 列出可选数据源、是否免费 / 需不需要 key、覆盖范围、符号写法与已知限制。
/// MCP tool type: list_data_sources — lists data sources, cost / key requirements, coverage, symbol formats and caveats.
/// </summary>
[McpServerToolType]
public static class ListDataSourcesTool
{
    /// <summary>返回数据源目录 JSON / Returns the data source catalog as JSON.</summary>
    [McpServerTool(Name = "list_data_sources"), Description(
        "List the market-data sources usable by run_backtest / optimize_strategy / walk_forward / fetch_ohlcv: which are FREE " +
        "with no API key (Yahoo, Binance, Demo, LocalFile), which need a free key (Tiingo, ...), what each covers, symbol formats, " +
        "how far back history goes, and known caveats. Call this first when the user has not said where data should come from. " +
        "Read-only, zero network.")]
    public static string ListDataSources()
    {
        var keyStatus = new Func<string, string>(env => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(env)) ? "not set" : "set");
        var json = $$"""
            {
              "recommendation": "Stocks/ETFs/indices: Yahoo (no key). Crypto: Binance (no key). Need a more stable US equity feed: Tiingo (free key). Own data: LocalFile.",
              "sources": [
                {
                  "name": "Yahoo", "cost": "free", "apiKey": "none",
                  "coverage": "US stocks & ETFs, global indices (^GSPC, ^NDX, ^HSI), Hong Kong (0700.HK), China A-shares (600519.SS / 000001.SZ), Japan (7203.T), FX (EURUSD=X), futures (GC=F, CL=F), crypto (BTC-USD)",
                  "history": "decades for most US tickers (SPY from 1993)",
                  "adjusted": "yes — OHLC back-adjusted for dividends and splits (total-return backtests)",
                  "caveats": "unofficial endpoint, may rate-limit (results are cached on disk); today's tickers only (survivorship bias)"
                },
                {
                  "name": "Binance", "cost": "free", "apiKey": "none",
                  "coverage": "crypto spot pairs: BTCUSDT, ETHUSDT, SOLUSDT … (BTC → BTCUSDT, BTC-USD → BTCUSDT automatically)",
                  "history": "from the pair's Binance listing (BTCUSDT from 2017-08)",
                  "adjusted": "n/a", "caveats": "24/7 market → ~365 bars per year; uses the public market-data mirror data-api.binance.vision"
                },
                {
                  "name": "Tiingo", "cost": "free tier", "apiKey": "TIINGO_API_KEY ({{keyStatus("TIINGO_API_KEY")}}) — https://www.tiingo.com/account/api/token",
                  "coverage": "US stocks & ETFs, end-of-day", "history": "~30 years", "adjusted": "yes",
                  "caveats": "free tier has hourly/daily request limits (cache helps); more stable than Yahoo"
                },
                {
                  "name": "LocalFile", "cost": "free", "apiKey": "none",
                  "coverage": "anything you have: CSV header date,open,high,low,close,volume[,adjusted_close] or JSON array",
                  "history": "your file", "adjusted": "as provided",
                  "caveats": "for several symbols pass a directory with SYMBOL.csv files or a template like C:/data/{symbol}.csv"
                },
                {
                  "name": "Demo", "cost": "free", "apiKey": "none", "coverage": "offline synthetic series (fixed 260 bars from startDate)",
                  "history": "synthetic", "adjusted": "n/a", "caveats": "for trying the tools only — never draw conclusions from it"
                },
                {
                  "name": "Finnhub", "cost": "free tier", "apiKey": "FINNHUB_API_KEY ({{keyStatus("FINNHUB_API_KEY")}})",
                  "coverage": "US stocks", "history": "varies", "adjusted": "no",
                  "caveats": "historical candles may require a paid plan; prefer Yahoo or Tiingo"
                },
                {
                  "name": "Fmp", "cost": "free tier", "apiKey": "FMP_API_KEY ({{keyStatus("FMP_API_KEY")}})",
                  "coverage": "US stocks", "history": "limited on free tier", "adjusted": "partly", "caveats": "250 requests/day on free tier"
                },
                {
                  "name": "TwelveData", "cost": "free tier", "apiKey": "TWELVEDATA_API_KEY ({{keyStatus("TWELVEDATA_API_KEY")}})",
                  "coverage": "stocks, FX, crypto", "history": "limited on free tier", "adjusted": "no", "caveats": "8 requests/minute on free tier"
                }
              ],
              "cache": "Downloaded bars are cached under %LOCALAPPDATA%/QuantInfraNet/cache (override with QUANT_INFRA_CACHE_DIR; disable with QUANT_INFRA_CACHE=off).",
              "notSupported": "Stooq (proven unstable). Live/real-time feeds are out of scope for research tools."
            }
            """;
        return json;
    }
}
