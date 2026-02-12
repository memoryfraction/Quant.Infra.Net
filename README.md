# Quant.Infra.Net

[![License](https://img.shields.io/github/license/memoryfraction/Quant.Infra.Net?color=blue)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Repo Size](https://img.shields.io/github/repo-size/memoryfraction/Quant.Infra.Net)](https://github.com/memoryfraction/Quant.Infra.Net)

**Quant.Infra.Net** is a professional quantitative trading infrastructure framework built on **.NET 8**. It is designed to provide a robust foundation for quant developers, covering market data ingestion, advanced statistical analysis, order execution, and automated monitoring.

---

## 🚀 Features (English)

### 1. Source Data Service
* **Multi-Market Support**: Integrated with **Yahoo Finance API** for global Equities, Options, Forex, and Crypto.
* **Flexible Frequencies**: Supports Daily, Weekly, and Monthly historical time series data.
* **Resilience**: Built-in request rate limiting and retry logic to ensure stability during large-scale data fetching.

### 2. Analysis Service
* **Statistical Tests**: Built-in **ADF (Augmented Dickey-Fuller)** test for time series stationarity, essential for statistical arbitrage.
* **Python Interoperability**: Seamlessly call Python libraries (NumPy, Pandas, Statsmodels) within C# via `Python.Runtime`.
* **High Performance**: Powered by `MathNet.Numerics` and `Accord.Statistics` for engineering-grade precision.

### 3. Order & Account Management
* **Binance Integration**: Deeply adapted for Binance Spot and Futures, supporting balance tracking and full order lifecycle.
* **Abstract Architecture**: Unified exchange interface allowing easy expansion to OKX, Interactive Brokers (IBKR), etc.

### 4. Notification System
* **Multi-Channel**: Built-in Webhook support for **DingTalk** and **Enterprise WeChat**.
* **Automated Alerting**: Real-time notifications for strategy signals, order execution, and system anomalies.

---

## 📖 中文简介

**Quant.Infra.Net** 是一个基于 **.NET 8** 构建的专业量化交易基础设施框架。本项目致力于为量化开发者提供稳健的底层支撑，旨在消除从策略原型到实盘交易之间的工程鸿沟。

### 核心模块详述

1. **数据源服务 (Source Data)**：集成 Yahoo Finance API，提供全球全标的历史行情抓取，支持标准化字段清洗与健壮的重试机制。
2. **统计分析模块 (Analysis)**：内置 ADF 平稳性检验及相关性分析。支持通过 `Python.Runtime` 在 C# 中直接调用 Python 科学计算库。
3. **交易管理中心 (Order)**：深度适配币安 (Binance) 现货与合约系统。采用统一接口设计，开发者可低成本扩展至其他交易平台。
4. **实时通知系统 (Notification)**：封装了钉钉与企业微信机器人 Webhook，支持场景化告警推送（如信号触发、完全成交、系统异常）。

---

## 💻 Quick Start / 快速上手

### 1. Market Data Ingestion / 行情获取
```csharp
var provider = new YahooFinanceProvider();
var history = await provider.GetHistoryAsync("AAPL", DateTime.Now.AddYears(-1), DateTime.Now);
// Access bars: bar.Open, bar.High, bar.Low, bar.Close...
