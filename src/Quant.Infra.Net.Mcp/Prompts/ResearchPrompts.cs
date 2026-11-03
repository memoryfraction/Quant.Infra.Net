using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Quant.Infra.Net.Mcp.Prompts;

/// <summary>
/// MCP 提示词模板：在 Claude Desktop 等客户端里以"一键工作流"出现，带用户完成从描述策略到稳健性验证的全过程。
/// MCP prompt templates: appear as one-click workflows in Claude Desktop and similar clients, guiding the user from a
/// plain-language strategy idea to a robustness-checked result.
/// </summary>
[McpServerPromptType]
public static class ResearchPrompts
{
    /// <summary>策略研究全流程 / Full strategy research workflow.</summary>
    [McpServerPrompt(Name = "strategy_research"), Description(
        "Turn a plain-language trading idea into a professional, robustness-checked backtest: pick data, map the idea to a strategy, " +
        "backtest vs benchmark, read the regime table, optimize with out-of-sample validation, then walk-forward.")]
    public static string StrategyResearch(
        [Description("The trading idea in your own words, e.g. 'hold the 2 strongest of SPY/QQQ/TLT/GLD, go to cash in bear markets'.")] string idea,
        [Description("Optional period, e.g. '2010 to today'.")] string? period = null)
        => $"""
            You are a careful quantitative researcher using the Quant.Infra.Net MCP tools. My idea: "{idea}".
            Period: {period ?? "as long as data allows (aim for 10+ years, include 2008, 2020 and 2022 if possible)"}.

            Work in this order and show your reasoning briefly at each step:
            1. Call list_strategies and list_data_sources. Map my idea to the closest strategy + parameters
               (MultiFactor for rotation / ranking / multi-asset ideas; MaCross for trend; MeanReversion for buy-the-dip;
               PairTradingZScore for spreads). If the mapping is approximate, say exactly what differs.
            2. Pick free data: Yahoo for stocks/ETFs/indices, Binance for crypto. Use a sensible benchmark (e.g. SPY).
            3. run_backtest. Summarize: CAGR, Sharpe, max drawdown vs the benchmark, the regime table (where it wins/loses),
               and every warning. Draw the equityCurveMonthly as a chart if you can, and give me the report path.
            4. optimize_strategy on 1-2 parameters that matter most (small grid, <= 30 combos). Report the in-sample best AND the
               most robust combination, and whether out-of-sample performance held up.
            5. walk_forward with the same grid (train 36 months / test 12 months unless the period is short). Tell me if the edge
               survives regime changes, how stable the chosen parameters are, and the walk-forward efficiency.
            6. Finish with a plain-language verdict: keep / modify / discard, the single biggest risk, and the next experiment.
            Rules: never present in-sample numbers as expected future returns; always compare to buy & hold; this is research,
            not investment advice.
            """;

    /// <summary>5 分钟上手 / 5-minute quick start.</summary>
    [McpServerPrompt(Name = "quickstart"), Description("A 5-minute guided tour: run a first real backtest on free data and open the report.")]
    public static string QuickStart()
        => """
            Give me a 5-minute tour of the Quant.Infra.Net MCP tools:
            1. Call list_strategies and list_data_sources and summarize them in a short table.
            2. Run run_backtest with strategy=MaCross, symbol=SPY, slowPeriod=200, dataSource=Yahoo, startDate=2005-01-01, endDate=today.
               Compare it to buy & hold SPY and explain the regime table in plain words.
            3. Run run_backtest with strategy=MultiFactor, universe="SPY,QQQ,IWM,EFA,EEM,TLT,GLD,DBC", factors="momentum=1,lowvol=0.5",
               topK=3, regimeSymbol=SPY, defensiveSymbol=SHY, benchmark=SPY, dataSource=Yahoo, startDate=2008-01-01, endDate=today.
            4. Show both report paths and suggest three follow-up questions I could ask in plain language.
            """;

    /// <summary>诊断风格切换 / Diagnose regime dependence.</summary>
    [McpServerPrompt(Name = "regime_check"), Description("Check whether a strategy only works in certain market regimes and how to make it more robust.")]
    public static string RegimeCheck(
        [Description("Strategy and parameters to check, e.g. 'MaCross on QQQ, slow 100'.")] string strategy)
        => $"""
            Check whether this strategy is regime-dependent: {strategy}.
            1. run_backtest over the longest free history available (Yahoo / Binance) with a benchmark.
            2. Explain the regime table (Bull/Bear × Calm/Volatile) and the rolling 6-month Sharpe chart in the report.
            3. walk_forward (no grid first, then with a small grid) to see how many out-of-sample windows are profitable.
            4. Propose up to two concrete robustness fixes, then TEST them: e.g. a regime filter (MultiFactor regimeSymbol=SPY),
               combining it with a strategy that wins in its weak regime, volatility-aware sizing (lowvol factor), or longer lookbacks.
            5. Report before/after in one table. Be honest if nothing helps.
            """;
}
