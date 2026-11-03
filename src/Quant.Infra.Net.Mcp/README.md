# Quant.Infra.Net.Mcp — backtest trading ideas in plain language

An MCP server for Claude Desktop, Claude Code, VS Code Copilot, Cursor and any other MCP client.
Describe a strategy in plain language and get a professional backtest with charts, then keep refining it in conversation.

- **Free data, no API key**: Yahoo (US/HK/China stocks, ETFs, indices, FX, futures) and Binance (crypto). Tiingo is optional (free key). You can also use your own CSV files.
- **Strategies**: trend (MaCross), mean reversion, pairs trading and **MultiFactor** rotation (momentum, low-vol, trend, reversal and risk-adjusted momentum, with a regime filter and a defensive asset).
- **Reports**: a self-contained HTML file with charts: equity vs benchmark, drawdown, rolling Sharpe, regime table, monthly heatmap and fills.
- **Robustness checks**: `optimize_strategy` (grid search plus out-of-sample holdout and an overfitting verdict) and `walk_forward` (rolling re-optimization that stitches the out-of-sample windows together).
- **Read-only**: the server never places live orders.

## Install

You need the .NET 8+ SDK. With .NET 10 you can skip the install step and run it through `dnx` (see the config below).

```bash
dotnet tool install -g Quant.Infra.Net.Mcp
```

## Configure your MCP client

Claude Desktop (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "quant-infra-net": { "command": "quant-infra-mcp" }
  }
}
```

With .NET 10 and no install step:

```json
{
  "mcpServers": {
    "quant-infra-net": { "command": "dnx", "args": ["Quant.Infra.Net.Mcp", "--yes"] }
  }
}
```

Claude Code: `claude mcp add quant-infra-net -- quant-infra-mcp`

## Try it

> Backtest a rotation over SPY, QQQ, IWM, EFA, EEM, TLT, GLD and DBC since 2008. Hold the 3 with the best momentum, prefer calmer ones, and go to SHY when SPY is below its 200-day average. Compare to SPY and show me the report.

> Which slow-MA period works best for QQQ? Check that it holds up out of sample.

> Does that strategy only work in bull markets? Run a walk-forward analysis.

Tools: `list_strategies`, `list_data_sources`, `run_backtest`, `optimize_strategy`, `walk_forward`, `fetch_ohlcv`, `run_paper_cycle`.
Prompts: `quickstart`, `strategy_research`, `regime_check`.

Docs: https://github.com/memoryfraction/Quant.Infra.Net · License: MIT · Research only, not investment advice.
