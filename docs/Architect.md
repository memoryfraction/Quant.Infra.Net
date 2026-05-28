# Quant.Infra.Net 系统架构文档

> 量化交易基础设施库 — 整体架构、数据流、层级结构及维护指南

---

## 文档版本控制

| 版本 | 日期 | 更新内容 | 更新人 |
|------|------|---------|--------|
| 2.0.0 | 2026-05-28 | 重写为全局架构文档，覆盖全部模块 | Quant.Infra.Net Team |
| 1.0.0 | 2026-04-19 | 初始邮件服务架构文档 | Quant.Infra.Net Team |

> **提示**：当您修改了代码后，请在此表末尾添加新行，更新版本号和日期，并简要描述变更。

---

## 目录

1. [系统概述](#1-系统概述)
2. [整体架构总览](#2-整体架构总览)
3. [核心模块详解](#3-核心模块详解)
   - 3.1 [SourceData — 数据采集层](#31-sourcedata--数据采集层)
   - 3.2 [Analysis — 统计分析层](#32-analysis--统计分析层)
   - 3.3 [Broker — 券商接入层](#33-broker--券商接入层)
   - 3.4 [Order — 订单管理层](#34-order--订单管理层)
   - 3.5 [Portfolio — 投资组合与绩效层](#35-portfolio--投资组合与绩效层)
   - 3.6 [Notification — 通知服务层](#36-notification--通知服务层)
   - 3.7 [Shared — 公共基础设施层](#37-shared--公共基础设施层)
4. [数据流](#4-数据流)
5. [设计模式应用](#5-设计模式应用)
6. [技术栈](#6-技术栈)
7. [配置与安全管理](#7-配置与安全管理)
8. [测试策略](#8-测试策略)
9. [维护指南](#9-维护指南)
10. [扩展指南](#10-扩展指南)

---

## 1. 系统概述

**Quant.Infra.Net** 是一个面向 .NET 生态的**量化交易基础设施库**，旨在为量化策略开发者提供开箱即用的数据、分析、交易、通知全套工具链。

### 1.1 核心理念

- **Don't reinvent the wheel** — 提供标准化的数据模型、统一的分析接口、可插拔的券商接入
- **SOLID 优先** — 所有模块遵循单一职责、开闭原则、接口隔离、依赖反转
- **生产就绪** — 支持 Testnet / Paper / Live 三级环境，凭据安全存储，完善的日志和异常处理
- **中英双语** — 所有公共接口提供中英双语文档注释，README 也同步维护中英文版本

### 1.2 解决的核心问题

| 问题域 | 解决方案 |
|--------|---------|
| 数据源分散、格式不统一 | `ITraditionalFinanceSourceDataService` / `ICryptoSourceDataService` 统一 OHLCV 模型 |
| 配对交易的统计分析重复编码 | `IAnalysisService` 一行调用：相关性、ADF、OLS、Z-Score、Shapiro-Wilk |
| 多券商接入的样板代码 | `IBinanceUsdFutureService`、`IUSEquityBrokerService` 等统一接口抽象 |
| 策略运行后没有通知管道 | 钉钉 `IDingtalkService`、企业微信 `IWeChatService`、邮件 `IEmailService` |
| 绩效指标每次手算 | `PortfolioCalculationService` 内置 CAGR、Sharpe、Calmar、最大回撤 |
| 定时器和滑动窗口重复造轮子 | `IntervalTrigger`、`RollingWindow<T>` 即开即用 |

### 1.3 解决方案结构

```
Quant.Infra.Net.sln
├── Quant.Infra.Net          ── 核心类库（类库项目）
├── Quant.Infra.Net.Tests    ── MSTest 单元测试项目
├── Quant.Infra.Net.Console  ── 控制台演示项目
└── MyQuantApp               ── 用户快速上手示例
```

---

## 2. 整体架构总览

### 2.1 分层架构图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           应用层 (Application Layer)                      │
│  ┌─────────────────┐  ┌──────────────────┐  ┌──────────────────────┐   │
│  │  Quant.Infra.Net│  │Quant.Infra.Net.  │  │    MyQuantApp        │   │
│  │  (类库)         │  │Console (控制台)   │  │    (用户应用示例)    │   │
│  └────────┬────────┘  └────────┬─────────┘  └──────────┬───────────┘   │
└───────────┼────────────────────┼───────────────────────┼────────────────┘
            │                    │                       │
            ▼                    ▼                       ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        服务接口层 (Service Interface Layer)               │
│                                                                         │
│  ┌─────────────┐ ┌──────────────┐ ┌───────────┐ ┌───────────────────┐  │
│  │ IAnalysis-  │ │ ITraditional-│ │ IBinance- │ │ IUSEquityBroker-  │  │
│  │ Service     │ │ Finance-     │ │ UsdFuture-│ │ Service           │  │
│  │             │ │ SourceData-  │ │ Service   │ │                   │  │
│  │             │ │ Service      │ │           │ │ ISchwabBroker-    │  │
│  │             │ │              │ │           │ │ Service           │  │
│  └──────┬──────┘ └──────┬───────┘ └─────┬─────┘ └────────┬──────────┘  │
│         │               │               │                │             │
│  ┌──────┴──────┐  ┌─────┴──────┐  ┌─────┴─────┐  ┌──────┴────────┐   │
│  │ IEmail-     │  │ IDingtalk- │  │ IWeChat-  │  │ IHistorical-  │   │
│  │ Service     │  │ Service    │  │ Service   │  │ DataSource-   │   │
│  │             │  │            │  │           │  │ Service       │   │
│  └─────────────┘  └────────────┘  └───────────┘  └───────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                        服务实现层 (Service Implementation Layer)          │
│                                                                         │
│  ┌────────────────────────┐ ┌────────────────────────────────────┐     │
│  │    Analysis 模块       │ │      SourceData 模块               │     │
│  │  ┌──────────────────┐  │ │  ┌────────────┐┌───────────────┐  │     │
│  │  │ AnalysisService   │  │ │  │ Yahoo/     ││ Binance/     │  │     │
│  │  │ SpreadCalculator  │  │ │  │ yfinance   ││ CoinMarketCap│  │     │
│  │  └──────────────────┘  │ │  └────────────┘└───────────────┘  │     │
│  │                        │ │  ┌────────────┐┌───────────────┐  │     │
│  │  Order 模块            │ │  │  CSV/Mysql ││ RealTime      │  │     │
│  │  ┌──────────────────┐  │ │  │  /MongoDB  ││ DataSource    │  │     │
│  │  │ BinanceOrder-    │  │ │  └────────────┘└───────────────┘  │     │
│  │  │ Service          │  │ └────────────────────────────────────┘     │
│  │  │ IBKRService      │  │                                            │
│  │  └──────────────────┘  │  ┌────────────────────────────────────┐    │
│  │                        │  │      Broker 模块                    │    │
│  │  Portfolio 模块        │  │  ┌──────────┐┌──────────────────┐  │    │
│  │  ┌──────────────────┐  │  │  │ Binance- ││ AlpacaClient/   │  │    │
│  │  │ PortfolioCalcu-  │  │  │  │ Service  ││ USEquityAlpaca  │  │    │
│  │  │ lationService    │  │  │  └──────────┘└──────────────────┘  │    │
│  │  └──────────────────┘  │  │  ┌──────────┐┌──────────────────┐  │    │
│  │                        │  │  │ Schwab-  ││ Interactive-    │  │    │
│  │  Notification 模块     │  │  │ Broker-  ││ BrokersService  │  │    │
│  │  ┌──────────────────┐  │  │  │ Service  ││ (InterReact)    │  │    │
│  │  │ PersonalEmail-   │  │  │  └──────────┘└──────────────────┘  │    │
│  │  │ Service          │  │  └────────────────────────────────────┘    │
│  │  │ CommercialEmail- │  │                                            │
│  │  │ Service          │  │                                            │
│  │  └──────────────────┘  │                                            │
│  └────────────────────────┘ └──────────────────────────────────────────┘
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         基础设施层 (Infrastructure Layer)                │
│                                                                         │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────┐ ┌───────────────┐  │
│  │ MailKit   │ │ Binance- │ │ Alpaca-  │ │ScottPlot│ │ pythonnet     │  │
│  │ MimeKit   │ │ .Net     │ │ Markets  │ │        │ │ (yfinance)    │  │
│  ├──────────┤ ├──────────┤ ├──────────┤ ├────────┤ ├───────────────┤  │
│  │ Accord    │ │ MathNet- │ │ CsvHelper│ │RestSharp│ │ MySql.Data /  │  │
│  │ .NET      │ │ Numerics │ │          │ │        │ │ MongoDB.Driver│  │
│  └──────────┘ └──────────┘ └──────────┘ └────────┘ └───────────────┘  │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 模块依赖关系

```
Shared (公共基础)
  ├── SourceData (数据采集) ← 依赖 Shared
  ├── Analysis (统计分析)   ← 依赖 Shared, SourceData (Model)
  ├── Broker (券商接入)     ← 依赖 Shared, SourceData (Model), Portfolio (Model)
  ├── Order (订单管理)      ← 依赖 Shared
  ├── Portfolio (投资组合)  ← 依赖 Shared, SourceData, Broker, Order
  └── Notification (通知)   ← 依赖 Shared
```

---

## 3. 核心模块详解

### 3.1 SourceData — 数据采集层

**职责**：提供统一的数据获取接口，支持多种数据源；包括历史数据和实时数据。

**文件位置**：`src/Quant.Infra.Net/SourceData/`

#### 3.1.1 模型层

| 类 | 说明 |
|----|------|
| [BasicOhlcv](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\SourceData\Model\Ohlcv.cs#L6) | OHLCV 基础模型（OpenDateTime, CloseDateTime, Open, High, Low, Close, Volume），带 CSVHelper 映射属性 |
| [Ohlcv](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\SourceData\Model\Ohlcv.cs#L44) | 继承 BasicOhlcv，增加 AdjustedClose；重写 Equals/GetHashCode 用于 HashSet 去重 |
| [Ohlcvs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\SourceData\Model\Ohlcvs.cs#L7) | 聚合容器，包含 Symbol、ResolutionLevel、时间范围、HashSet\<Ohlcv\> |
| [CoinMarketCap Models](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\SourceData\Model\CoinMarketCapModels.cs) | CMC API 响应反序列化模型 |

#### 3.1.2 历史数据服务

```
IHistoricalDataSourceService (接口)
  ├── IHistoricalDataSourceServiceCryptoBinance  ── Binance 历史数据
  ├── IHistoricalDataSourceServiceCryptoMySql    ── MySQL 历史数据
  ├── IHistoricalDataSourceServiceTraditionalFinance ── 传统金融数据
  └── 实现类:
      ├── HistoricalDataSourceServiceCsv         ── CSV 读取
      ├── HistoricalDataSourceServiceMongodb     ── MongoDB 读取
      └── HistoricalDataSourceServiceMySql       ── MySQL 读取
```

#### 3.1.3 传统金融数据服务

[ITraditionalFinanceSourceDataService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\SourceData\Service\ITraditionalFinanceSourceDataService.cs)
- `DownloadOhlcvListAsync()` — 从 Yahoo Finance API 下载 OHLCV 数据（C# 版本）
- `BeginSyncSourceDailyDataAsync()` — 全量同步并落盘到 CSV
- `GetSp500SymbolsAsync()` — 从 Wikipedia 获取 S&P 500 成分股列表

#### 3.1.4 加密货币数据服务

[ICryptoSourceDataService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\SourceData\Service\CryptoSourceDataService.cs#L21)
- `GetTopMarketCapSymbolsFromCoinMarketCapAsync()` — 从 CoinMarketCap 获取市值排名
- `DownloadBinanceSpotAsync()` — 批量下载币安现货 K 线，支持增量更新
- `DownloadBinanceUsdFutureAsync()` — 批量下载币安 U 本位合约 K 线

#### 3.1.5 实时数据服务

[IRealtimeDataSourceService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\SourceData\Service\RealTime\IRealtimeDataSourceService.cs)
- `GetLatestPriceAsync()` — 获取指定标的最新价格
- `GetOhlcvListAsync(endDt, limit)` — 获取最近 N 根 K 线

#### 3.1.6 IO 服务

[IOService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\SourceData\Service\IOService.cs)
- `ReadCsv()` / `WriteCsvManually()` — CSV 文件读写
- 基于 CsvHelper，支持自定义列映射

#### 3.1.7 Python 集成数据采集

本项目同时通过 [pythonnet](https://github.com/pythonnet/pythonnet) 调用 Python `yfinance` 库获取 Yahoo Finance 数据，以解决 C# 版 YahooFinanceApi 频繁返回 401 的问题。

```csharp
// 在 MyQuantApp/Program.cs 中演示
using (Py.GIL())
{
    dynamic yf = Py.Import("yfinance");
    dynamic df = yf.download("AAPL", start: "2025-01-01", end: "2026-01-01", auto_adjust: true);
    // 处理 df 中的 Close 列
}
```

---

### 3.2 Analysis — 统计分析层

**职责**：提供配对交易所需的统计分析工具，同时支持 C# 原生和 Python 两种计算后端。

**文件位置**：`src/Quant.Infra.Net/Analysis/`

#### 3.2.1 核心接口

[IAnalysisService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Analysis\Service\IAnalysisService.cs)

| 方法 | 说明 | 后端 |
|------|------|------|
| `CalculateCorrelation()` | Pearson 相关性 | MathNet.Numerics |
| `AugmentedDickeyFullerTest(IEnumerable, threshold)` | ADF 平稳性检验（布尔结果） | 纯 C# 实现 |
| `AugmentedDickeyFullerTest(IEnumerable)` | ADF 检验（返回 Statistic + PValue） | 纯 C# 实现 |
| `AugmentedDickeyFullerTestPython()` | ADF 检验（通过 Python statsmodels） | pythonnet |
| `PerformOLSRegression()` | OLS 线性回归 | MathNet.Numerics |
| `PerformShapiroWilkTest()` | Shapiro-Wilk 正态性检验 | Accord.NET |
| `CalculateZScores()` | Z-Score 计算 | 纯 C# 实现 |

#### 3.2.2 Spread Calculator（价差计算器）

```
SpreadCalculatorFixLength (抽象基类)
  ├── SpreadCalculatorUsEquity           ── 美股价差（半年=126个交易日）
  └── SpreadCalculatorPerpetualContract  ── 永续合约价差（半年=183天）
```

核心功能：
- 协整回归窗口期（CointegrationFixedWindowLength）
- 半衰期计算（HalfLifeWindowLength）
- 支持 DataFrame 输入输出（Microsoft.Data.Analysis）

---

### 3.3 Broker — 券商接入层

**职责**：统一封装不同券商的交易 API，提供一致的操作接口。支持 Testnet / Paper / Live 三种环境切换。

**文件位置**：`src/Quant.Infra.Net/Broker/`

#### 3.3.1 架构设计

```
BrokerServiceBase (抽象基类)
  ├── BinanceService ── 通用币安服务（工厂方法创建）
  │
  IBinanceSpotService       ── 币安现货交易
  ├── BinanceSpotService
  └── InMemoryBinanceBrokerService ── 内存模拟（测试用）
  
  IBinanceUsdFutureService  ── 币安U本位合约
  ├── BinanceUsdFutureService
  
  IUSEquityBrokerService    ── 美股交易
  ├── USEquityAlpacaBrokerService (Alpaca API)
  
  ISchwabBrokerService      ── Schwab 券商
  ├── SchwabBrokerService
  
  InteractiveBrokersService ── 盈透证券 (InterReact 客户端)
  
  BrokerServiceFactory      ── 工厂类，根据 Broker 枚举创建服务
```

#### 3.3.2 关键功能

- **账户查询**：`GetAccountAsync()` / `GetAccountEquityAsync()`
- **持仓管理**：`GetHoldingPositionAsync()` / `HasPositionAsync()` / `GetPositionAsync()`
- **下单执行**：`PlaceOrderAsync()` / `SetHoldingsAsync()` / `SetUsdFutureHoldingsAsync()`
- **清仓**：`LiquidateAsync()` / `LiquidateUsdFutureAsync()`
- **市场状态**：`IsMarketOpeningAsync()` / `IsMarketOpenAsync()`
- **环境切换**：`ExchangeEnvironment` 属性（Testnet / Paper / Live）

#### 3.3.3 Schwab 附加功能

[ISchwabBrokerService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Broker\Interfaces\ISchwabBrokerService.cs)
- `GetQuoteAsync()` / `GetQuotesAsync()` — 实时报价
- `GetOptionChainAsync()` — 期权链（含 Greeks：Delta/Gamma/Theta/Vega/Rho）
- `GetOrdersAsync()` — 历史订单查询
- `CancelOrderAsync()` — 撤单

---

### 3.4 Order — 订单管理层

**职责**：提供统一的订单数据模型和订单服务接口。

**文件位置**：`src/Quant.Infra.Net/Order/`

#### 3.4.1 订单模型体系

```
OrderBase (基类)
  ├── OrderBinanceSpot              ── 币安现货订单（QuoteOrderQty 支持）
  ├── OrderBinancePerpetualContract ── 币安永续合约（StopPrice、TrailingDelta、Leveraged）
  └── OrderIBKR                     ── 盈透证券订单

OrderFactory ── 根据交易所名称创建对应订单对象
```

#### 3.4.2 订单服务

- [IBinanceOrderService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Order\Service\IBinanceOrderService.cs) — 币安订单全生命周期管理（下单、查询、撤单、替换、批量撤单）
  - 支持现货和 U 本位永续合约
  - `SetBinanceCredential()` 支持动态切换 API Key
  - `LiquidateAsync()` 一键清仓
  - `ReplaceSpotOrderAsync()` 订单替换（Cancel-Replace 模式）
  
- [IIBKRService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Order\Service\IIBKRService.cs) — IBKR 订单服务

---

### 3.5 Portfolio — 投资组合与绩效层

**职责**：管理投资组合快照，计算持仓市值、未实现盈亏，绘制净值曲线。

**文件位置**：`src/Quant.Infra.Net/Portfolio/`

#### 3.5.1 模型体系

```
PortfolioBase (抽象基类)
  ├── StockPortfolio   ── 股票投资组合（BaseCurrency = USD）
  └── CryptoPortfolio  ── 加密货币投资组合（BaseCurrency = USDT）

PortfolioSnapshot ── 时点快照（DateTime + Balance + Positions）
Balance           ── 余额模型（NetLiquidationValue, MarketValue, Cash, UnrealizedPnL）
Position          ── 持仓模型（Symbol, Quantity, CostPrice, AssetType）
Positions         ── 持仓集合
```

#### 3.5.2 核心功能

- **快照管理**：`UpsertSnapshot(dateTime, balance, positions)` — 插入或更新时点快照
- **市值追踪**：`UpdateMarketValues(symbolOhlcvDic)` — 基于 OHLCV 数据回算每日市值
- **未实现盈亏**：`TotalUnrealisedProfitAsync()` — 基于实时价格计算浮动盈亏
- **图表绘制**：`DrawChart()` — 使用 ScottPlot 绘制市值随时间变化图
- **持仓计算**：[PortfolioCalculationService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Portfolio\Services\PortfolioCalculationService.cs)
  - `CalculateBalance()` — 输入 cash + 最新价格，输出 Balance
  - `CalculatePositions()` — 输入已成交订单，更新持仓（含加权平均成本）

---

### 3.6 Notification — 通知服务层

**职责**：多渠道消息推送，支持策略运行结果通知。

**文件位置**：`src/Quant.Infra.Net/Notification/`

#### 3.6.1 架构设计（策略模式 + 工厂模式）

```
IEmailService (策略接口)
  ├── PersonalEmailService    ── 个人邮箱（126/QQ/Gmail/Outlook，SSL 465）
  └── CommercialEmailService  ── 商业邮件（Brevo SMTP，STARTTLS 587）

EmailServiceFactory ── 根据配置 Email:Type (Personal/Commercial) 路由

IDingtalkService    ── 钉钉机器人通知
IWeChatService      ── 企业微信机器人通知
```

#### 3.6.2 邮件配置结构

```json
{
  "Email": {
    "Type": "Commercial",
    "Personal": { "SmtpServer": "smtp.126.com", "Port": 465 },
    "Commercial": { "SmtpServer": "smtp-relay.brevo.com", "Port": 587 }
  }
}
```

凭据通过 `dotnet user-secrets` 或环境变量注入，禁止硬编码。

---

### 3.7 Shared — 公共基础设施层

**职责**：提供全项目共享的基础模型、工具类、扩展方法。

**文件位置**：`src/Quant.Infra.Net/Shared/`

#### 3.7.1 核心模型

| 类 | 说明 |
|----|------|
| [Underlying](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Model\Underlying.cs) | 标的资产（Symbol + AssetType） |
| [RollingWindow\<T\>](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Model\RollingWindow.cs) | 泛型定长滑动窗口（Queue 实现，自动移除最旧元素） |
| [BasicOhlcvRollingWindow\<T\>](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Model\BasicOhlcvRollingWindow.cs) | OHLCV 滑动窗口，带时间范围约束 |
| [TimeSeries / TimeSeriesElement](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Model\TimeSeries.cs) | 时间序列模型 |
| [OLSRegressionData](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Model\OLSRegressionData.cs) | OLS 回归数据封装 |
| [PythonInfraModel / PythonNetInfra](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Model\PythonInfra.cs) | Python 运行时环境配置 |

#### 3.7.2 核心枚举

所有枚举集中在 [Shared/Model/Enums.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Model\Enums.cs)：

| 枚举 | 说明 |
|------|------|
| `ExchangeEnvironment` | Testnet=0 / Live=1 / Paper=2 |
| `StartMode` | 定时触发模式（NextSecond/Minute/Hour/Day/TodayBeforeUSMarketClose） |
| `Broker` | 支持的券商：Binance / InteractiveBrokers / Alpaca / Schwab |
| `OrderStatus` | 订单状态：New / PartiallyFilled / Filled / Canceled / Rejected |
| `AssetType` | 资产类型：UsEquity / CryptoSpot / CryptoPerpetualContract / Option |
| `ResolutionLevel` | 时间分辨率：Minute / Hourly / Daily / Weekly / Monthly |
| `DataSource` | 数据源：YahooFinance / MongoDBWebApi / CSV / Binance |
| `Currency` | 币种：USD / USDT / BTC / ETH / CNY / HKD |

#### 3.7.3 公共工具服务

| 服务 | 说明 |
|------|------|
| [UtilityService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Service\UtilityService.cs) | 日志（Serilog）、Python 脚本执行、目录文件操作 |
| [IntervalTrigger](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Service\IntervalTrigger.cs) | 高精度定时触发器，支持秒/分/时/日/美股收盘前模式 |
| [ResolutionConversionService](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Service\ResolutionConversionService.cs) | 时间周期转换（如分钟到日线聚合） |

#### 3.7.4 扩展方法

| 扩展类 | 说明 |
|--------|------|
| [DateTimeExtension](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Extension\DateTimeExtension.cs) | 日期时间工具扩展 |
| [DataFrameExtensions](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Extension\DataFrameExtensions.cs) | DataFrame 操作扩展 |
| [AssetTypeExtensions](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Extension\AssetTypeExtensions.cs) | 资产类型转换扩展 |
| [AlpacaMarketsExtension](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\Shared\Extension\AlpacaMarketsExtension.cs) | Alpaca API 适配扩展 |

---

## 4. 数据流

### 4.1 完整策略执行数据流

```
┌──────────┐    ┌──────────────┐    ┌──────────┐    ┌──────────┐    ┌──────────────┐
│  数据采集  │───▶│  统计分析    │───▶│  交易策略  │───▶│  执行下单  │───▶│  持仓更新    │
│          │    │              │    │          │    │          │    │              │
│ Yahoo/   │    │ Correlation │    │ Z-Score  │    │ Binance/ │    │ Portfolio   │
│ Binance/ │    │ ADF / OLS   │    │ 阈值判断  │    │ Alpaca/  │    │ Snapshot    │
│ CSV/DB   │    │ Spread      │    │ 开/平仓   │    │ Schwab   │    │ Balance     │
└──────────┘    └──────────────┘    └──────────┘    └──────────┘    └──────┬───────┘
                                                                          │
                                                                          ▼
                                                                   ┌──────────────┐
                                                                   │  通知推送     │
                                                                   │              │
                                                                   │ 钉钉/微信/邮件│
                                                                   └──────────────┘
```

### 4.2 数据获取详细流程

```
开始
  │
  ▼
构建 Underlying(symbol, assetType)
  │
  ▼
选择数据源：
  ├── 传统金融 → ITraditionalFinanceSourceDataService
  │                ├── DataSource.YahooFinance → YahooFinanceApi (C#)
  │                └── Python yfinance → pythonnet 调用
  ├── 加密货币 → ICryptoSourceDataService
  │                ├── CoinMarketCap → 获取市值排名
  │                ├── Binance Spot → K线数据下载
  │                └── Binance Future → 合约K线下载
  └── 本地数据 → IOService.ReadCsv()
                   └── 或 HistoricalDataSourceServiceMySql/Mongodb
  │
  ▼
返回 Ohlcvs (标准化模型)
  │
  ▼
缓存到 CSV（增量更新机制，避免重复下载）
```

### 4.3 交易执行流程

```
策略决策（如：Z-Score > 2 开仓）
  │
  ▼
BrokerServiceFactory.CreateBrokerService(Broker.Binance)
  │
  ▼
IBinanceUsdFutureService.SetUsdFutureHoldingsAsync("BTCUSDT", 0.1, PositionSide.Long)
  │
  ▼
BinanceUsdFutureService 内部:
  ├── 验证环境 (ExchangeEnvironment)
  ├── 获取当前持仓
  ├── 计算目标数量
  └── 调用 Binance.Net API 下单
  │
  ▼
PortfolioCalculationService.CalculatePositions(portfolio, filledOrder)
  │
  ▼
Portfolio.UpsertSnapshot(UtcNow, balance, positions)
  │
  ▼
Notification: 钉钉/微信/邮件通知结果
```

---

## 5. 设计模式应用

### 5.1 策略模式 (Strategy Pattern)

- **IEmailService** + PersonalEmailService / CommercialEmailService
- 统一接口，多种实现，运行时动态切换

### 5.2 工厂模式 (Factory Pattern)

- **EmailServiceFactory** — 根据配置类型返回邮件服务
- **BrokerServiceFactory** — 根据 Broker 枚举创建券商服务
- **OrderFactory** — 根据交易所名称创建订单模型

### 5.3 抽象基类模式 (Template Method)

- **BrokerServiceBase** — 定义交易操作的抽象模板，子类实现具体逻辑
- **PortfolioBase** — 定义投资组合管理的通用骨架
- **SpreadCalculatorFixLength** — 定义价差计算的标准流程

### 5.4 依赖注入 (Dependency Injection)

- 全项目通过 `IServiceCollection` 注册服务
- 支持 Scoped / Transient 生命周期
- 便于单元测试 Mock

### 5.5 接口隔离原则 (ISP)

- `IHistoricalDataSourceService` 细分为 CryptoBinance / CryptoMySql / TraditionalFinance 子接口
- `IRealtimeDataSourceService` 细分为 Crypto / TraditionalFinance 子接口
- 避免胖接口，客户端只依赖自己需要的接口

### 5.6 AutoMapper 映射

[MappingProfile](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net\MappingProfile.cs) — 统一管理外部库模型与内部模型的映射关系

```csharp
CreateMap<Candle, Ohlcv>().ReverseMap();  // YahooFinanceApi → 内部Ohlcv
```

---

## 6. 技术栈

### 6.1 运行时

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 8.0 | 运行时框架 |
| C# 语言版本 | 10.0 | 编程语言 |
| MSTest | 3.x | 单元测试框架 |
| Microsoft.Data.Analysis | - | DataFrame 数据处理 |

### 6.2 数据采集

| 包 | 用途 |
|----|------|
| YahooFinanceApi | Yahoo Finance C# 客户端 |
| pythonnet + yfinance | Python 版 Yahoo Finance 数据采集 |
| Binance.Net | 币安 API 封装（现货 + 合约） |
| CoinMarketCap API | 加密货币市值排名 |

### 6.3 统计分析

| 包 | 用途 |
|----|------|
| Accord.NET | Shapiro-Wilk 检验 |
| MathNet.Numerics | 相关性、OLS 回归、线性代数 |
| statsmodels (Python) | ADF 检验（Python 后端） |

### 6.4 券商接入

| 包 | 用途 |
|----|------|
| Binance.Net | 币安交易 |
| Alpaca.Markets | 美股交易 |
| InterReact | 盈透证券交易 |
| Schwab API (自定义封装) | Schwab 交易 |

### 6.5 通知与IO

| 包 | 用途 |
|----|------|
| MailKit / MimeKit | 邮件发送 |
| RestSharp | HTTP 客户端（钉钉/微信通知） |
| CsvHelper | CSV 读写 |
| MySql.Data / MongoDB.Driver | 数据库访问 |
| AutoMapper | 对象映射 |
| ScottPlot | 净值曲线图表绘制 |
| Serilog | 结构化日志 |

---

## 7. 配置与安全管理

### 7.1 配置体系

配置文件：`appsettings.json`（模板见 `appsettings.example.json`）

```
配置层级:
appsettings.json          ── 公共配置（无敏感信息）
appsettings.*.json        ── 环境特定配置（被 .gitignore 排除）
dotnet user-secrets       ── 开发环境敏感信息
环境变量                  ── 生产环境敏感信息
```

### 7.2 敏感数据保护

| 数据类型 | 开发环境 | 生产环境 |
|----------|---------|---------|
| Binance API Key/Secret | User Secrets | 环境变量 |
| Alpaca API Key | User Secrets | 环境变量 |
| 邮件密码 | User Secrets | 环境变量 |
| 数据库连接串 | appsettings.json（本地） | 环境变量 |

### 7.3 环境切换

`ExchangeEnvironment` 枚举控制交易环境：

```csharp
var service = new BinanceUsdFutureService(configuration)
{
    ExchangeEnvironment = ExchangeEnvironment.Testnet  // 测试网
};
```

| 环境 | 说明 |
|------|------|
| Testnet | 测试网，无真实资金风险 |
| Paper | 模拟盘，使用虚拟资金 |
| Live | 实盘，真实资金交易 |

### 7.4 代码规范强制项

详见 [CODE_STANDARDS.md](file://e:\Github\Quant.Infra.Net\docs\CODE_STANDARDS.md)：

- ✅ 所有 public 成员必须有中英文 XML 文档
- ✅ 运行时输出必须为英文（避免乱码）
- ✅ 所有时间持久化使用 `DateTime.UtcNow`
- ✅ 所有枚举集中在 `Shared/Model/Enums.cs`
- ✅ 所有 public 方法开头必须做参数校验

---

## 8. 测试策略

### 8.1 测试项目

`Quant.Infra.Net.Tests` — 基于 MSTest 框架

### 8.2 测试覆盖范围

| 测试文件 | 覆盖模块 |
|----------|---------|
| [AnalysisTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\AnalysisTests.cs) | 统计分析服务 |
| [BinanceTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\BinanceTests.cs) | 币安现货服务 |
| [BinanceUsdFutureTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\BinanceUsdFutureTests.cs) | 币安合约服务 |
| [CryptoSourceDataServiceTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\CryptoSourceDataServiceTests.cs) | 加密货币数据服务 |
| [DataSourceServiceTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\DataSourceServiceTests.cs) | 传统金融数据服务 |
| [PortfolioTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\PortfolioTests.cs) | 投资组合计算 |
| [NotificationTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\NotificationTests.cs) | 通知服务 |
| [EmailServiceTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\EmailServiceTests.cs) | 邮件服务 |
| [SchwabBrokerServiceTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\SchwabBrokerServiceTests.cs) | Schwab 券商 |
| [USEquityAlpacaBrokerServiceTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\USEquityAlpacaBrokerServiceTests.cs) | Alpaca 美股 |
| [RollingWindowTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\RollingWindowTests.cs) | 滑动窗口 |
| [IntervalTriggerTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\IntervalTriggerTests.cs) | 定时触发器 |
| [PythonNetTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\PythonNetTests.cs) | Python 集成 |
| [SpreadCalculatorCSharpTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\SpreadCalculatorCSharpTests.cs) | C# 价差计算 |
| [SpreadCalculatorPythonTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\SpreadCalculatorPythonTests.cs) | Python 价差计算 |
| [YahooFinanceApiUnitTests.cs](file://e:\Github\Quant.Infra.Net\src\Quant.Infra.Net.Tests\YahooFinanceApiUnitTests.cs) | Yahoo Finance |

### 8.3 运行测试

```bash
# 运行所有测试
dotnet test

# 运行指定测试
dotnet test --filter "FullyQualifiedName~AnalysisTests"
```

---

## 9. 维护指南

### 9.1 目录结构说明

```
src/
├── Quant.Infra.Net/               ── 核心类库项目
│   ├── Analysis/                   ── 统计分析模块
│   │   ├── Models/                 ── 分析模型
│   │   └── Service/                ── 分析服务
│   ├── Broker/                     ── 券商接入模块
│   │   ├── Interfaces/             ── 券商接口
│   │   ├── Models/                 ── 券商模型
│   │   └── Service/                ── 券商实现
│   ├── Notification/               ── 通知模块
│   │   ├── Model/                  ── 通知模型
│   │   └── Service/                ── 通知实现
│   ├── Order/                      ── 订单模块
│   │   ├── Model/                  ── 订单模型
│   │   └── Service/                ── 订单服务
│   ├── Portfolio/                  ── 投资组合模块
│   │   ├── Models/                 ── 组合模型
│   │   └── Services/               ── 组合服务
│   ├── SourceData/                 ── 数据采集模块
│   │   ├── Model/                  ── 数据模型
│   │   └── Service/                ── 数据服务
│   │       ├── Historical/         ── 历史数据实现
│   │       └── RealTime/           ── 实时数据接口
│   └── Shared/                     ── 公共模块
│       ├── Extension/              ── 扩展方法
│       ├── Model/                  ── 公共模型（含所有枚举）
│       └── Service/                ── 公共工具服务
├── Quant.Infra.Net.Tests/          ── 测试项目
├── Quant.Infra.Net.Console/        ── 控制台演示
└── MyQuantApp/                     ── 用户示例
```

### 9.2 添加新功能的原则

1. **新增数据源**
   - 实现 `IHistoricalDataSourceService` 子接口或 `ITraditionalFinanceSourceDataService`
   - 在 `DataSource` 枚举中添加新数据源类型
   - 注册到 DI 容器

2. **新增券商**
   - 定义专用接口（如 `IBinanceUsdFutureService`）
   - 实现具体服务类
   - 在 `Broker` 枚举中添加新类型
   - 在 `BrokerServiceFactory` 中注册
   - 编写单元测试

3. **新增通知渠道**
   - 定义接口（如 `IDingtalkService`）
   - 实现具体服务
   - 编写单元测试

4. **新增分析算法**
   - 在 `IAnalysisService` 中添加方法签名
   - 在 `AnalysisService` 中实现
   - 编写单元测试

### 9.3 更新架构文档

每次对**模块结构、数据流、依赖关系、设计模式**有重大变更时，请同步更新本文档：

1. 在**文档版本控制**表中添加新行
2. 更新对应模块的架构图（如果变化较大）
3. 更新数据流说明（如果有新流程）
4. 更新设计模式说明（如果有新模式引入）
5. 更新测试策略（如果有新测试）

### 9.4 常见维护任务

| 任务 | 操作 |
|------|------|
| 更新 API Key | 使用 `dotnet user-secrets set` 或环境变量，**不要**修改 appsettings.json |
| 修复数据源变更 | 修改对应实现类（如 YahooFinanceApi 接口变更） |
| 增加交易对支持 | 检查对应的 `GetSymbolsAsync()` 方法和数据同步逻辑 |
| 调整时间分辨率 | 使用 `ResolutionConversionService` 进行周期聚合 |
| 调试定时触发 | 查看日志中的 `[IntervalTrigger]` 输出，确认下次触发时间 |

### 9.5 常见问题排查

| 问题 | 可能原因 | 解决方案 |
|------|---------|---------|
| Yahoo Finance 返回 401 | C# YahooFinanceApi 接口变更 | 切换到 Python yfinance 后端 |
| Binance API 连接失败 | IP 区域限制 | 检查 Binance 官方限制列表 |
| 邮件发送失败 | 凭据错误或 SMTP 配置不对 | 确认 Email:Type 配置和 User Secrets |
| 策略未触发 | IntervalTrigger 时间计算错误 | 检查 StartMode 和 DelayTimeSpan |
| Python 调用失败 | Conda 环境路径不对 | 确认 PythonDllName 和 CondaEnvPath |

---

## 10. 扩展指南

### 10.1 添加新的券商服务

```csharp
// 1. 定义接口
public interface IMyNewBrokerService
{
    Task<decimal> GetAccountBalanceAsync();
    Task PlaceOrderAsync(string symbol, decimal quantity);
}

// 2. 实现服务
public class MyNewBrokerService : IMyNewBrokerService
{
    // 实现细节
}

// 3. 枚举扩展（在 Shared/Model/Enums.cs）
public enum Broker
{
    Binance = 0,
    InteractiveBrokers = 1,
    Alpaca = 2,
    Schwab = 3,
    MyNewBroker = 4  // 新增
}

// 4. 注册工厂（在 BrokerServiceFactory）
public BrokerServiceBase CreateBrokerService(Broker brokerType)
{
    return brokerType switch
    {
        Broker.MyNewBroker => new MyNewBrokerService(),
        // ...
    };
}
```

### 10.2 添加新的通知渠道

```csharp
// 1. 定义接口
public interface ISlackService
{
    Task<RestResponse> SendMessageAsync(string channel, string message);
}

// 2. 实现服务
public class SlackService : ISlackService { /* ... */ }

// 3. 注册到 DI
services.AddTransient<ISlackService, SlackService>();
```

### 10.3 添加新的数据源

```csharp
// 1. 枚举扩展
public enum DataSource
{
    YahooFinance = 0,
    MongoDBWebApi = 1,
    CSV = 2,
    Binance = 3,
    MyNewSource = 4
}

// 2. 在 TraditionalFinanceSourceDataService.DownloadOhlcvListAsync()
//    或 CryptoSourceDataService 中添加对应分支
if (dataSource == DataSource.MyNewSource)
{
    // 新数据源实现
}
```

---

## 架构优势总结

1. **分层清晰** — 数据采集、分析、交易、通知各层职责明确，模块间通过接口解耦
2. **可扩展** — 工厂模式 + 策略模式 + 接口隔离，新增数据源/券商/通知几乎零侵入
3. **可测试** — 依赖注入 + 接口抽象，所有模块均可独立 Mock 测试
4. **生产就绪** — 三级环境切换、凭据安全存储、Serilog 日志、智能错误处理
5. **开发者友好** — 中英文双语注释、完整的代码规范、丰富的示例代码
6. **跨语言集成** — pythonnet 桥接 Python 生态（yfinance、statsmodels），兼得 .NET 性能和 Python 生态

---

**最后更新**: 2026-05-28
**版本**: 2.0.0
**维护者**: Quant.Infra.Net Team

---

> **Disclaimer**: See [DISCLAIMER.md](DISCLAIMER.md) for full disclaimer and limitation of liability / 详见 [免责声明](DISCLAIMER.md) 了解完整免责条款与责任限制。
