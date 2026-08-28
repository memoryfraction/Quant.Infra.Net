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

**Core Capabilities / 核心基础设施:**

| Module | What It Does / 能力说明 |
|--------|------------------------|
| **Data Source / 数据源** | Unified market data ingestion from Yahoo Finance & Binance (Spot/Futures), with local CSV/SQL persistence. <br>聚合多源行情（Yahoo/Binance），并支持本地持久化。 |
| **Broker & Orders / 订单执行** | Standardized trading interfaces for Binance Futures, seamlessly switching between testnet simulation and live execution. <br>币安合约标准化交易接口，无缝切换测试网模拟与实盘下单。 |
| **Notification / 通知推送** | Real-time strategy alerts via DingTalk bots, WeChat Work webhooks, and SMTP/Brevo email pipelines. <br>内置钉钉、企业微信及邮件通道，实现策略信号的即时触达。 |

> For full module details and usage examples, see [User Manual](docs/Manual.md) and [Architecture Overview](docs/Architect.md).

---

## Architecture / 架构

```
┌─────────────────────────────────────────────────────────────────┐
│                     Your Strategy Logic                          │
│                  (Write once, run anywhere)                       │
└──────────────────────┬──────────────────────────────────────────┘
┌──────────────────────────────────────────────────────────────────┐
│  Orchestration Layer (Quant.Infra.Net.Orchestration)             │
│  DataIngest → Analysis → Signal → TargetPosition → Risk          │
│  → Execution (Paper broker, zero network) → PortfolioState       │
│  → Notification   PipelineRunner + AddQuantInfraNetOrchestration │
└───────────────────────────────┬──────────────────────────────────┘
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
| [Orchestration Layer Design / 编排层设计](docs/OrchestrationLayerDesign.md) | E2E orchestration: signal generation, risk gate, Paper execution, pipeline & DI |

---

> 📖 [GitHub Pages](https://memoryfraction.github.io/Quant.Infra.Net/) — full documentation site

## Related Projects / 相关项目

| Project | Description |
|---|---|
| [LLSDA](https://github.com/memoryfraction/LLSDA-Lightning-Location-System-Data-Analyzer) | Open-source lightning location system (LLS) data analysis library — published on NuGet, cited in a TechRxiv preprint. 开源闪电定位系统数据分析类库 —— 已发布 NuGet 包，并被 TechRxiv 预印本引用。 |
| [HealthData-Interoperability-Csharp](https://github.com/memoryfraction/HealthData-Interoperability-Csharp) | AI-driven FHIR R4/R5 healthcare interoperability engine for .NET — HIPAA compliance helpers, US Core conformance, local AI semantic validation. 基于 .NET 的 AI 驱动 FHIR R4/R5 医疗数据互操作引擎 —— HIPAA 合规辅助、US Core 一致性校验、本地 AI 语义验证。 |

> More projects by the same author: [github.com/memoryfraction](https://github.com/memoryfraction) / 同一作者的更多项目

---

> **Disclaimer**: See [DISCLAIMER](docs/Disclaimer.md) for full disclaimer and limitation of liability / 详见免责声明了解完整免责条款与责任限制。
