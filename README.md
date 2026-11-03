<div align="center">

# Quant.Infra.Net

**Ask a trading question in plain language → get data, backtests, tuned parameters and an honest answer.**
**用一句大白话提出交易想法 → 拿回数据、回测、调好的参数，和一个诚实的答案。**

[![Build & Test](https://github.com/memoryfraction/Quant.Infra.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/memoryfraction/Quant.Infra.Net/actions/workflows/ci.yml)  [![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)  [![MCP](https://img.shields.io/badge/MCP_server-1.6.0-orange.svg)](https://www.nuget.org/packages/Quant.Infra.Net.Mcp)  [![Runtime](https://img.shields.io/badge/Runtime-1.6.0-green.svg)](https://www.nuget.org/packages/Quant.Infra.Net.Runtime)  [![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## 🌐 Choose your language / 选择语言

### [📖 English →](docs/readme-en.md) &nbsp;&nbsp;&nbsp;|&nbsp;&nbsp;&nbsp; [📖 中文 →](docs/readme-ch.md)

</div>

---

> 🏆 **Proof / 实证：** a CLEC (James) allocation, **30% QQQ / 30% QLD / 40% cash, rebalanced yearly**, found by searching 88 real-data backtests (2007–2026) and checked on years it never saw. It beat the S&P 500 on **CAGR 14.5% vs 10.6%**, **Sharpe 0.81 vs 0.61** and **Calmar 0.31 vs 0.19**. The study also shows the first rule that *failed*. → [English](docs/readme-en.md#-proof-a-clec-james-allocation-that-beat-the-sp-500-on-cagr-sharpe-and-calmar) · [中文](docs/readme-ch.md)
>
> 一个 CLEC（James 老师）配置：**30% QQQ / 30% QLD / 40% 现金，每年再平衡**。它来自 88 次真实数据回测（2007–2026）的搜索，并在从未见过的年份上做了检验，**年化 14.5% 对 10.6%、夏普 0.81 对 0.61、卡玛 0.31 对 0.19**，全面跑赢标普 500。研究里也如实展示了*失败*的第一条规则。

| 💬 Natural language<br/>自然语言 | 🔎 Find data<br/>找数据 | ⏱️ Backtest<br/>回测 | 🎛️ Tune<br/>调参 | 🏆 Best params & strategy<br/>找出最优参数和策略 |
|:---:|:---:|:---:|:---:|:---:|
| Talk to Claude / Copilot / Cursor through the built-in MCP server<br/>通过内置 MCP server 直接和 AI 对话 | Free, no-key Yahoo & Binance data, cached<br/>免费免 key 的 Yahoo 与币安数据，本地缓存 | Bar by bar, next-bar fills, HTML report vs benchmark<br/>逐 bar 回放、下一根成交、图文报告对比基准 | Grid search with an out-of-sample holdout<br/>网格调参 + 样本外检验 | Walk-forward: trust what survives unseen data<br/>滚动前推：只信样本外活下来的 |

**Try it in one minute / 一分钟试用**（[.NET 8+ SDK](https://dotnet.microsoft.com/download)）:

```bash
dotnet tool install -g Quant.Infra.Net.Mcp
```

```bash
claude mcp add quant-infra-net -- quant-infra-mcp
```

> *"Which moving-average period works best for QQQ, and does it still hold out of sample?"*
> *「QQQ 用多少日均线最好？样本外还成立吗？」*

---

> ⚖️ **Markets change and nobody can predict the future.** This project gives you *methods* to diagnose, validate and stress-test a strategy, not a strategy that is promised to keep working. Not investment advice. / **市场风格随时会切换，没有人能预测未来。** 本项目提供诊断、验证、压力测试的*方法*，不提供"永远有效"的策略。不构成投资建议。 [Disclaimer / 免责声明](docs/DISCLAIMER.md)

<div align="center">

Built by **Rong Fan** · [@memoryfraction](https://github.com/memoryfraction) · EPAT® (QuantInsti) · [Alpha Wealth Lab](https://www.alpha-wealth-lab.com/planner/)

⭐ **Star the repo if it helps you. / 有帮助的话请点个 Star。**

[🐞 Issues](https://github.com/memoryfraction/Quant.Infra.Net/issues/new) · [💡 Discussions](https://github.com/memoryfraction/Quant.Infra.Net/discussions) · [💼 Work with me / 商务合作](https://calendly.com/rex-fan18/30min) · MIT License

</div>
