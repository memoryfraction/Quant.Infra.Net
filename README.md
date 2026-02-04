# Quant.Infra.Net

[![License](https://img.shields.io/github/license/memoryfraction/Quant.Infra.Net?color=blue)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Repo Size](https://img.shields.io/github/repo-size/memoryfraction/Quant.Infra.Net)](https://github.com/memoryfraction/Quant.Infra.Net)

---

# 中文版 README

## 📖 项目简介
**Quant.Infra.Net** 是一个基于 **.NET 8** 构建的专业量化交易基础设施框架。本项目致力于为量化开发者提供稳健的底层支撑，涵盖行情接入、深度统计分析、实盘订单执行及自动化监控告警，旨在消除从策略原型到实盘交易之间的工程鸿沟。



## 🚀 核心功能详述

### 1. 数据源服务 (Source Data Service)
* **多标的市场接入**：集成 **Yahoo Finance API**，支持获取全球美股正股、期权、外汇以及主流加密货币的行情。
* **灵活频率抓取**：支持日线 (Daily)、周线 (Weekly)、月线 (Monthly) 历史序列，满足长短周期不同维度的策略回测。
* **标准化数据字段**：提供包含 `Open`, `High`, `Low`, `Close`, `Adj Close`, `Volume` 的全字段清洗后数据。
* **健壮性设计**：内置 API 请求频率限制处理与重试逻辑，确保大规模标的数据拉取时的系统稳定性。

### 2. 统计分析模块 (Analysis Service)
* **时间序列平稳性检验**：内置 **ADF (Augmented Dickey-Fuller) 检验**，支持对价格序列或配对价差进行单位根检验，是统计套利策略的基础。
* **多维相关性分析**：支持多资产相关系数矩阵计算，辅助进行投资组合去相关性与风险暴露控制。
* **Python 跨语言互操作**：通过 `Python.Runtime` 桥接，支持在 C# 环境中直接调用成熟的 Python 科学计算库（如 NumPy, Pandas, Statsmodels）。
* **高性能计算底座**：底层依赖 `Accord.Statistics` 与 `MathNet.Numerics`，保证工程级的数值计算精度与性能。

### 3. 交易管理中心 (Order & Account Management)
* **币安 (Binance) 深度适配**：
    * **账户与资产监控**：支持现货 (Spot) 与 合约 (Futures) 的账户余额、持仓详情、保证金率等实时查询。
    * **全生命周期订单管理**：提供创建、撤销、批量处理及成交状态追踪（支持市价单、限价单及高级订单类型）。
* **抽象化架构**：设计了统一的交易所抽象层，开发者可基于此框架低成本扩展对接 OKX、盈透证券 (IBKR) 等交易所。

### 4. 实时通知系统 (Notification)
* **多渠道机器人接入**：内置钉钉 (DingTalk) 与企业微信 (WeChat) 机器人 Webhook 封装。
* **场景化告警推送**：支持策略信号触发、订单完全成交/部分成交提醒、以及系统运行时的关键异常告警。

## 🏗 项目结构说明
```text
Quant.Infra.Net
├── Analysis/           # 统计分析 (ADF检验、相关性矩阵、Python调用逻辑)
├── Notification/       # 通知分发 (钉钉与微信机器人 Webhook 适配)
├── Order/              # 交易执行 (Binance API 深度对接与账户状态管理)
├── Shared/             # 共享库 (通用实体模型、常量定义、接口规范)
└── SourceData/         # 数据获取 (Yahoo Finance 适配器与数据清洗层)
```

# Quant.Infra.Net (English)

[![License](https://img.shields.io/github/license/memoryfraction/Quant.Infra.Net?color=blue)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)

**Quant.Infra.Net** is an industrial-grade quantitative trading infrastructure framework built on **.NET 8**. It provides a robust foundation for market data ingestion, advanced statistical analysis, order execution, and automated monitoring.



## 🚀 Detailed Features

### 1. Source Data Service
* **Global Asset Coverage**: Integrated with **Yahoo Finance API** for U.S. Stocks, Options, Forex, and Cryptocurrencies.
* **Flexible Frequencies**: Supports historical sequences in Daily, Weekly, and Monthly timeframes.
* **Standardized Output**: Provides cleaned data fields including `Open`, `High`, `Low`, `Close`, `Adj Close`, and `Volume`.
* **Resilience**: Features built-in request rate limiting and retry logic for stable data fetching.

### 2. Analysis Service
* **Time Series Analysis**: Built-in **ADF (Augmented Dickey-Fuller) Test** for analyzing price or spread stationarity.
* **Correlation Matrices**: Calculates multi-asset correlation coefficients to manage portfolio risk exposure.
* **Python Interoperability**: Bridged via `Python.Runtime`, enabling the use of Python libraries (NumPy, Pandas, Statsmodels) directly from C#.
* **Mathematical Precision**: Powered by `Accord.Statistics` and `MathNet.Numerics` for engineering-grade accuracy.

### 3. Order & Account Management
* **Deep Binance Integration**: Supports real-time balance tracking, position monitoring, and full order lifecycle management (Spot & Futures).
* **Abstract Architecture**: Unified interface design, ready for expansion to other exchanges like OKX or Interactive Brokers (IB).

### 4. Notification System
* **Multi-Channel Delivery**: Built-in support for DingTalk and WeChat bot Webhooks.
* **Automated Alerting**: Instant notifications for strategy triggers, execution receipts, and system anomalies.

## 🏗 Project Structure
```text
Quant.Infra.Net
├── Analysis/           # Statistical analysis & Python interoperability
├── Notification/       # DingTalk & WeChat notification logic
├── Order/              # Order execution & Binance API integration
├── Shared/             # Common models and abstractions
└── SourceData/         # Yahoo Finance data adapters
```
