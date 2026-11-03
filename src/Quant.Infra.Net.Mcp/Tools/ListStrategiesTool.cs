using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Quant.Infra.Net.Mcp.Tools;

/// <summary>
/// MCP 工具类型：<c>list_strategies</c> —— 列出 Quant.Infra.Net 中可按名字调用的全部内置策略。
/// MCP tool type: <c>list_strategies</c> — lists every built-in strategy that can be invoked by name.
/// </summary>
[McpServerToolType]
public static class ListStrategiesTool
{
    /// <summary>
    /// 返回全部内置策略名（小写不敏感排序）及其参数说明。
    /// Returns all built-in strategy names (case-insensitive order) with their parameter documentation.
    /// </summary>
    /// <returns>JSON 字符串：策略名数组 + 每个策略的参数说明 / JSON string: strategy name array + per-strategy parameter documentation.</returns>
    [McpServerTool, Description(
        "List all built-in trading strategies available in Quant.Infra.Net " +
        "(names you can pass to run_backtest / run_paper_cycle). " +
        "Returns strategy names plus a short parameter description for each. " +
        "Read-only, zero network.")]
    public static Task<string> ListStrategies()
    {
        const string json = """
            {
              "strategies": [
                {
                  "name": "MaCross",
                  "description": "Moving-average cross: close vs slow SMA. Long when fast line >= slow line, else Flat (Short only if AllowShort=true).",
                  "parameters": { "Symbol": "required, e.g. AAPL", "FastPeriod": "int, default 1 (close)", "SlowPeriod": "int, default 200", "AllowShort": "bool, default false" }
                },
                {
                  "name": "MeanReversion",
                  "description": "Rolling-window z-score mean reversion: Long when z <= -EntryZ, Short/Flat when z >= +EntryZ, Flat when |z| <= ExitZ.",
                  "parameters": { "Symbol": "required, e.g. AAPL", "LookbackBars": "int, default 100", "EntryZ": "double, default 2.0", "ExitZ": "double, default 0.5", "AllowShort": "bool, default true" }
                },
                {
                  "name": "PairTradingZScore",
                  "description": "Pairs trading on the z-score of the spread between SymbolA and SymbolB; enters when |z| >= EntryZ, exits when |z| <= ExitZ.",
                  "parameters": { "SymbolA": "required, e.g. KO", "SymbolB": "required, e.g. PEP", "LookbackBars": "int, default 100", "EntryZ": "double, default 2.0", "ExitZ": "double, default 0.5" }
                },
                {
                  "name": "MultiFactor",
                  "description": "Cross-sectional multi-factor rotation. Every RebalanceDays bars, score each symbol in Universe by a weighted sum of factor z-scores, hold the TopK best equal-weight. Optional absolute-momentum filter (skip names with momentum <= 0) and regime filter (RegimeSymbol below its SMA -> cash or DefensiveSymbol). Use it for rotation / ranking / dual-momentum / risk-parity-lite / multi-asset ideas.",
                  "factors": {
                    "momentum": "trailing return over MomentumLookback bars, skipping the last SkipRecent bars (12-1 momentum: MomentumLookback=252;SkipRecent=21)",
                    "lowvol": "negative daily-return volatility over VolLookback (prefers calmer assets)",
                    "trend": "close / SMA(TrendPeriod) - 1",
                    "reversal": "negative 5-bar return (short-term mean reversion)",
                    "riskadjmom": "momentum divided by volatility (Sharpe-like momentum)"
                  },
                  "parameters": {
                    "Universe": "required, 2+ symbols, e.g. SPY,QQQ,IWM,EFA,EEM,TLT,GLD,DBC",
                    "Factors": "weights, default momentum=1, e.g. momentum=1,lowvol=0.5",
                    "TopK": "int, default round(universe/3)",
                    "RebalanceDays": "int, default 21",
                    "MomentumLookback": "int, default 126", "SkipRecent": "int, default 0",
                    "VolLookback": "int, default 63", "TrendPeriod": "int, default 200",
                    "AbsMomentumFilter": "bool, default true",
                    "RegimeSymbol": "optional, e.g. SPY", "RegimeSmaPeriod": "int, default 200",
                    "DefensiveSymbol": "optional, e.g. SHY / TLT / GLD"
                  }
                }
              ],
              "sizing": "Default position size: 100% for single-symbol strategies, 50% per pair leg, 100%/TopK for MultiFactor (override with positionSizePct).",
              "notes": "Custom strategies discovered from your own assembly are NOT listed here (the MCP server only knows the built-in catalog); pass any valid name to run_backtest / run_paper_cycle."
            }
            """;
        return Task.FromResult(json);
    }
}
