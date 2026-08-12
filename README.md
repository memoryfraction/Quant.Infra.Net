# Quant.Infra.Net

[![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)  [![Version](https://img.shields.io/badge/Version-1.5.1-blue.svg)](https://github.com/memoryfraction/Quant.Infra.Net/releases)  [![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **Quant.Infra.Net** is a .NET quantitative trading infrastructure library — data acquisition, statistical analysis, broker integration, portfolio analytics, and notifications in one package.

---

## Languages / 语言

- [English](docs/readme-en.md)
- [中文](docs/readme-ch.md)

---

## What Is This? / 这是什么？

Quant.Infra.Net abstracts the complexity of connecting to financial data sources, brokers, and notification channels behind a unified C# API. You write strategy logic once — the library handles the rest.

**Currently includes (当前功能模块):**

| Module | Current Implementations |
|--------|------------------------|
| **Data Source / 数据源** | Yahoo Finance (via yfinance/pythonnet), Binance Spot/Futures K-lines, CSV/MySQL/MongoDB readers |
| **Notification / 通知推送** | DingTalk bot (钉钉机器人), WeChat Work webhook (企业微信), personal/commercial email (SMTP + Brevo) |
| **Broker & Orders / 订单执行** | Binance Futures (testnet/live switching), Alpaca US Equity, Charles Schwab, Interactive Brokers (via InterReact) |

> For full module details and usage examples, see [User Manual](docs/Manual.md) and [Architecture Overview](docs/Architect.md).

---

## Architecture / 架构

```
┌─────────────────────────────────────────────────────────────────┐
│                     Your Strategy Logic                          │
│                  (Write once, run anywhere)                       │
└──────────────────────┬──────────────────────────────────────────┘
                       │ IQuantInfraNet API
   ┌───────────────────┼──────────────────────────────────────────┐
   │                   │                                          │
   ▼                   ▼                                          ▼
┌──────────┐    ┌──────────────┐                          ┌──────────────┐
│  Source  │    │   Broker     │                          │ Notification │
│  Data    │    │   & Orders   │                          │              │
│          │    │              │                          │              │
│ Yahoo    │    │ Binance      │                          │ DingTalk     │
│ Finance  │    │ Futures      │                          │ WeChat Work  │
│ Binance  │    │ Alpaca       │                          │ Email (SMTP) │
│ Spot/Perp│    │ Schwab       │                          │ Brevo        │
│ CSV/SQL  │    │ Interactive  │                          │              │
└──────────┘    │ Brokers      │                          └──────────────┘
                │ (Testnet/Live)│
                └──────────────┘
```

**Why this matters / 为什么重要:**
- **One NuGet package** — no juggling multiple SDKs from different vendors
- **Unified interfaces** — `ITraditionalFinanceSourceDataService`, `IBrokerService`, `IEmailService` — swap implementations without changing your strategy code
- **Out-of-the-box analysis** — ADF test, OLS regression, Z-Score, Sharpe ratio — all included

---

## Quick Start / 快速开始

```bash
# Install via NuGet
dotnet add package Quant.Infra.Net --version 1.5.1
```

```csharp
// Register all modules
var services = new ServiceCollection();
services.AddQuantInfraNet();

// Fetch OHLCV data from Yahoo Finance
var dataService = services.BuildServiceProvider()
    .GetService<ITraditionalFinanceSourceDataService>();
var bars = await dataService.GetOhlcvListAsync("AAPL", DateTime.Now.AddDays(-30), DateTime.Now);

// Place order via Binance Futures (testnet)
var binance = services.BuildServiceProvider()
    .GetService<IBinanceUsdFutureService>();
await binance.SetUsdFutureHoldingsAsync("BTCUSDT", 0.01, PositionSide.Long);

// Send notification alert when strategy triggers
var dingTalk = services.BuildServiceProvider()
    .GetService<IDingtalkService>();
await dingTalk.SendNotificationAsync("[Alert] BTC long position opened", token, secret);
```

---

## Documentation / 文档

| Document | Description |
|----------|-------------|
| [User Manual / 使用手册](docs/Manual.md) | Installation, module usage guide, API examples |
| [Architecture Overview / 架构概览](docs/Architect.md) | System design, module relationships, data flow |
| [Code Standards / 代码规范](docs/CodeStandard.md) | SOLID principles, XML docs, naming conventions, checklist |

---

> 📖 [GitHub Pages](https://memoryfraction.github.io/Quant.Infra.Net/) — full documentation site

> **Disclaimer**: See [DISCLAIMER](docs/Disclaimer.md) for full disclaimer and limitation of liability / 详见免责声明了解完整免责条款与责任限制。
