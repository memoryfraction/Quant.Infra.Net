# Quant.Infra.Net

[![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)  [![Version](https://img.shields.io/badge/Version-1.5.1-blue.svg)](https://github.com/memoryfraction/Quant.Infra.Net/releases)  [![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **Quant.Infra.Net** is a .NET quantitative trading infrastructure library — data acquisition, statistical analysis, broker integration, portfolio analytics, and notifications in one package.

> 📖 [Documentation / GitHub Pages](https://memoryfraction.github.io/Quant.Infra.Net/)

---

## What Is This?

Quant.Infra.Net provides a unified C# API that abstracts away the complexity of connecting to multiple financial data sources, brokers, and notification channels. Instead of writing separate integrations for each platform, you get consistent interfaces and ready-to-use implementations.

### Architecture Overview

| Module | Responsibility | Key Interfaces / Services |
|--------|---------------|---------------------------|
| **SourceData** | Multi-source market data ingestion | `ITraditionalFinanceSourceDataService`, `ICryptoSourceDataService` — Yahoo Finance (via yfinance/pythonnet), Binance spot/futures klines, Alpaca US equity, CSV/MySQL/MongoDB readers |
| **Broker** | Unified broker execution layer | `IBrokerService`, `IUSEquityBrokerService` — Binance Futures (spot/order/liquidate with Testnet/Paper/Live switching), Alpaca US Equity, Charles Schwab (quotes/options/orders/positions), Interactive Brokers via InterReact (TWS/Gateway) |
| **Analysis** | Quantitative/statistical tooling | `IAnalysisService` — ADF stationarity test, OLS regression, Z-Score, Shapiro-Wilk normality test, pair-trading spread calculation, rolling statistics |
| **Portfolio** | Position tracking and performance | `PortfolioSnapshot`, `StrategyPerformanceAnalyzer` — CAGR, Sharpe ratio, Calmar ratio, max drawdown, equity curve charting (ScottPlot) |
| **Notification** | Strategy alert dispatch | `IDingtalkService`, `IWeChatService`, `IEmailService` — DingTalk bot, WeChat Work webhook, personal/commercial bulk email |
| **Order** | Order modeling and lifecycle | Unified order models across brokers, order state machine, fill tracking |
| **Shared** | Cross-cutting utilities | `IntervalTrigger`, `RollingWindow<T>`, resolution conversion helpers, extension methods, DataFrame I/O (Deedle) |

---

## Why Use This Library?

### Pain Points in Quant Development

When building quantitative trading systems, most developers encounter these challenges:

| Challenge | What Happens Without This Library | How Quant.Infra.Net Solves It |
|-----------|----------------------------------|-------------------------------|
| **Data source fragmentation** | Each API (Yahoo, Binance, Alpaca, Schwab) returns data in its own format — you write converters for every provider | Unified `ITraditionalFinanceSourceDataService` and `ICryptoSourceDataService` with standardized OHLCV models; new sources are just another implementation of the same interface |
| **Broker boilerplate** | Connecting to Binance futures requires handling API keys, rate limits, WebSocket reconnects; Schwab requires OAuth flow; IB needs TWS/Gateway IPC | Single `IBrokerService` abstraction — swap brokers by changing configuration, not code |
| **Reinventing analysis math** | Implementing ADF tests, regressions, Z-Score normalization from scratch every time | `IAnalysisService` provides 10+ statistical methods ready to call |
| **No alerting pipeline** | Strategies run silently — you only discover results after hours of waiting | Built-in DingTalk, WeChat Work, and email notifications fire on strategy events |
| **Performance tracking is manual** | Computing CAGR, Sharpe, max drawdown requires writing formulas that may contain bugs | `StrategyPerformanceAnalyzer` implements standard metrics with unit tests; ScottPlot integration for charting |

### Who Is This For?

- Quantitative researchers and traders building strategies on the .NET platform
- Developers who want a single NuGet package to handle data, execution, and alerting
- Teams that need consistent broker abstractions across Binance, Alpaca, Schwab, and Interactive Brokers
- Anyone tired of writing the same integration code for every new project

---

## Quick Start

### Step 1: Install via NuGet

```bash
# Create a project (or use an existing one)
dotnet new console -n MyQuantApp
cd MyQuantApp

# Add the library
dotnet add package Quant.Infra.Net --version 1.5.1

# Required for Python-based data sources (Yahoo Finance via yfinance)
dotnet add package pythonnet

# Recommended for dependency injection
dotnet add package Microsoft.Extensions.DependencyInjection
```

### Step 2: Use in Code

```csharp
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Notification.Service;
using Microsoft.Extensions.DependencyInjection;

// Register services via DI
var services = new ServiceCollection();
services.AddQuantInfraNet();  // registers all modules

// --- Data: Fetch OHLCV from multiple sources ---
var dataService = services.BuildServiceProvider()
    .GetService<ITraditionalFinanceService>();
var bars = await dataService.GetOhlcvListAsync("AAPL", DateTime.Now.AddDays(-30), DateTime.Now);

// --- Analysis: Pair-trading correlation & ADF test ---
var analysis = services.BuildServiceProvider()
    .GetService<IAnalysisService>();
var correlation = await analysis.CalculateCorrelationAsync(aaplPrices, msftPrices);
var isStationary = await analysis.TestStationarityAsync(spreadSeries);

// --- Broker: Place orders across platforms ---
var broker = services.BuildServiceProvider()
    .GetService<IBrokerService>();
var orderResult = await broker.PlaceOrderAsync(new OrderRequest { Symbol = "AAPL", Side = Side.Buy, Quantity = 10 });

// --- Portfolio: Performance analytics ---
var portfolio = services.BuildServiceProvider()
    .GetService<IPortfolioSnapshotService>();
var snapshot = await portfolio.GetSnapshotAsync(accountId);

// --- Notification: Strategy alerts ---
var dingTalk = services.BuildServiceProvider()
    .GetService<IDingtalkService>();
await dingTalk.SendStrategyAlert("Mean reversion triggered for AAPL/MSFT spread");
```

### Step 3: Configuration

```json
// appsettings.json
{
  "BinanceApi": {
    "ApiKey": "your-api-key",
    "SecretKey": "your-secret-key",
    "Environment": "testnet"   // testnet | paper | live
  },
  "YahooFinance": {
    "PythonPath": "C:\\Users\\you\\Anaconda3\\python.exe"
  }
}
```

---

## Version History

| Version | Date | Description |
|---------|------|-------------|
| **1.5.1** *(current)* | 2026-08-12 | Code_Standards.md compliance — bilingual XML documentation on all public members, parameter validation audit, version alignment |
| 1.5.0 | 2026-05-28 | **Interactive Brokers (InterReact)** full integration — order, market data, account management via TWS/Gateway; **Charles Schwab** full broker service — quotes, option chains, orders, positions; license changed to MIT; enhanced analysis service unit tests |
| 1.4.0 | 2024-05-16 | Updated API integrations to handle recent broker changes, added comprehensive documentation |
| 1.3.0 | 2024-04-05 | Enhanced notification services with email templates and improved error handling |
| 1.2.0 | 2024-03-10 | Improved Python integration stability and added new statistical analysis methods |
| 1.1.0 | 2024-02-20 | Added support for Schwab broker integration and enhanced portfolio performance metrics |
| 1.0.0 | 2024-01-15 | Initial release with core features: data acquisition, statistical analysis, trade execution, and notifications |

---

## Code Standards

This project follows the coding standards defined in [docs/CodeStandard.md](docs/CodeStandard.md):
- Bilingual (Chinese + English) XML documentation on all public members
- SOLID principles for design
- Parameter validation on all entry points
- UTC time handling and consistent enum management

---

## Notes on Testing

> ⚠️ **Binance Unit Tests**: The Binance integration tests require a Singapore IP address to pass. They will fail when run from China or the United States due to regional access restrictions on Binance API endpoints. Run `dotnet test` excluding Binance tests for other modules:
>
> ```bash
> dotnet test --filter "FullyQualifiedName!~Binance"
> ```

---

## Ecosystem

| Project | Description |
|---------|-------------|
| **Quant.Infra.Net** (this repo) | Core quantitative trading library — data, analysis, execution, notifications |
| [**Quant.Infra.Net.Pro**](https://github.com/memoryfraction/Quant.Infra.Net.Pro) | Production-grade Charles Schwab web application with unattended OAuth token management and full dashboard |

---

## License

[MIT](LICENSE) — © 2024–2026 Rong (Rex) Fan

> **Disclaimer**: See [DISCLAIMER.md](docs/DISCLAIMER.md) for full disclaimer and limitation of liability / 详见 [免责声明](docs/DISCLAIMER.md) 了解完整免责条款与责任限制。


