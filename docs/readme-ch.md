# Quant.Infra.Net

[![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)  [![Version](https://img.shields.io/badge/Version-1.5.1-blue.svg)](https://github.com/memoryfraction/Quant.Infra.Net/releases)  [![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **Quant.Infra.Net** 是一个 .NET 量化交易基础库 —— 数据获取、统计分析、券商集成、组合分析和通知推送，一个 NuGet 包全部搞定。

> 📖 [文档 / GitHub Pages](https://memoryfraction.github.io/Quant.Infra.Net/)

---

## 这是什么？

Quant.Infra.Net 提供统一的 C# API，将连接多个金融数据源、券商和通知渠道的复杂性封装起来。你只需要写一次策略逻辑——剩下的交给库来处理。

**Core Capabilities / 核心基础设施:**

| Module | What It Does / 能力说明 |
|--------|------------------------|
| **Data Source / 数据源** | Unified market data ingestion from Yahoo Finance & Binance (Spot/Futures), with local CSV/SQL persistence. <br>聚合多源行情（Yahoo/Binance），并支持本地持久化。 |
| **Broker & Orders / 订单执行** | Standardized trading interfaces for Binance Futures, seamlessly switching between testnet simulation and live execution. <br>币安合约标准化交易接口，无缝切换测试网模拟与实盘下单。 |
| **Notification / 通知推送** | Real-time strategy alerts via DingTalk bots, WeChat Work webhooks, and SMTP/Brevo email pipelines. <br>内置钉钉、企业微信及邮件通道，实现策略信号的即时触达。 |

> 完整模块详情和使用示例，请参阅 [使用手册](Manual.md) 和 [架构概览](Architect.md)。

### 架构概览

| 模块 | 职责 | 关键接口 / 服务 |
|--------|---------------|---------------------------|
| **SourceData** | 多源市场数据接入 | `ITraditionalFinanceSourceDataService`, `ICryptoSourceDataService` — Yahoo Finance (通过 yfinance/pythonnet)、Binance 现货/期货 K 线、Alpaca 美股、CSV/MySQL/MongoDB 读取器 |
| **Broker** | 统一券商执行层 | `IBrokerService`, `IUSEquityBrokerService` — Binance Futures（现货/下单/清仓，支持测试网/模拟盘/实盘切换）、Alpaca 美股、Charles Schwab（报价/期权链/订单/持仓）、Interactive Brokers（通过 InterReact 连接 TWS/Gateway） |
| **Analysis** | 量化统计工具 | `IAnalysisService` — ADF 平稳性检验、OLS 回归、Z-Score、Shapiro-Wilk 正态性检验、配对交易价差计算、滚动统计 |
| **Portfolio** | 持仓跟踪和绩效分析 | `PortfolioSnapshot`, `StrategyPerformanceAnalyzer` — CAGR、夏普比率、卡尔玛比率、最大回撤、权益曲线图表（ScottPlot） |
| **Notification** | 策略通知推送 | `IDingtalkService`, `IWeChatService`, `IEmailService` — 钉钉机器人、企业微信 Webhook、个人/商业批量邮件 |
| **Order** | 订单建模和生命周期 | 跨券商的统一订单模型、订单状态机、成交跟踪 |
| **Shared** | 横切工具类 | `IntervalTrigger`, `RollingWindow<T>`, 分辨率转换辅助、扩展方法、DataFrame I/O（Deedle） |

---

## 编排层 Orchestration Layer（Beta）

`Quant.Infra.Net.Orchestration` 把上面这些独立模块串成一条可直接运行的管道：`数据采集 → 统计分析 → 信号生成 → 目标仓位 → 风控前置检查 → 执行调仓 → 组合状态更新 → 通知推送`。不用自己写胶水代码，注册一个扩展方法、选一个内置策略即可。

**3 个内置策略**（改一个配置值即可切换，零代码）：

| 策略 | `Parameters.Strategy` | 风格 |
|------|------------------------|------|
| 配对交易 z-score | `PairTradingZScore` | 统计套利（OLS 价差 + z-score） |
| 经典 200 日均线 | `MaCross` | 趋势跟踪 |
| 均值回归 z-score | `MeanReversion` | 震荡 / 均值回归 |

**一分钟内跑起来** —— Demo 默认用单标的 `MaCross` 策略，跑在进程内生成的合成 `AAPL` 序列上（零网络、零 API Key、不是真实行情——具体跑的是什么见下文）：

```bash
git clone https://github.com/memoryfraction/Quant.Infra.Net.git
cd Quant.Infra.Net/src
dotnet run --project Quant.Infra.Net.Orchestration.Console
```

控制台会打印完整事件流：数据采集 → 信号生成 → 风控检查 → Paper 模拟执行 → 组合快照。单标的意味着一条信号 / 一条目标仓位 / 一条执行报告，整个运行过程肉眼就能核对。修改 `Quant.Infra.Net.Orchestration.Console/appsettings.json` 即可切换策略或标的。

**Demo 具体用的什么数据源、什么标的、什么策略？怎么换成自己的？** 完整拆解 + 接入真实数据源/更换标的/自定义策略的分步说明，见 [编排层详细使用说明](OrchestrationQuickStart-ch.md)。

**接入自己的宿主程序**（`Environment` 默认 `Paper`——纯内存、零网络请求，默认安全）：

```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<OrchestrationOptions>(builder.Configuration.GetSection("Orchestration"));
builder.Services.AddQuantInfraNetOrchestration();   // Paper 环境 + 按 "Strategy" 参数自动装配管道

var host = builder.Build();
await host.RunAsync();
```

扩展点：向 `AddQuantInfraNetOrchestration(...)` 传入 `customStages`、`customSignalGenerator` 或 `customExecutionModel`，即可用自己的实现替换默认管道的任意一环。

切实盘需要两步显式操作，任何路径都不会默认触达实盘：在配置里把 `"Environment"` 改成 `"Testnet"` 或 `"Live"`，并在调用 `AddQuantInfraNetOrchestration()` 之前自行注册实盘 `IBinanceUsdFutureService`（该方法只会自动注册 Paper 模拟券商）。

完整契约（接口签名、里程碑、风控默认值、扩展点）见 [编排层设计文档](OrchestrationLayerDesign.md)。

---

## 回测引擎 Backtest Engine（Beta）

`Quant.Infra.Net.Backtest` 把历史数据回放进**同一条**管道：逐 bar、同一套 8 阶段 `StrategyPipeline`、同一份策略代码——零网络、零实盘券商、结构性杜绝未来函数。回测永远是"同一份策略代码在历史上重放"，而不是另写一套"回测专用实现"。

**一分钟内跑起来** —— Demo 用 120 根合成日线跑 `MaCross`（零网络、零 API Key；序列是合成的，不是真实 AAPL 数据）：

```bash
git clone https://github.com/memoryfraction/Quant.Infra.Net.git
cd Quant.Infra.Net
dotnet run --project src/Quant.Infra.Net.Backtest.Console
```

预期输出（确定性，节选）：

```
回测完成 / Backtest complete: 100 bars, 8 trades
CAGR=16.56%   Sharpe=0.55   Calmar=45.19
MaxDrawdown=-0.37%（9 天 / days）
WinRate=80.0%   ProfitFactor=23.12   Commission=0 USD
```

**能力一览：**

| 能力 | 说明 |
|---|---|
| **事件驱动回放** | 每根 bar 一次 `StrategyPipeline.RunAsync`——与 Paper 完全相同的代码路径，在历史上重放 |
| **无未来函数** | `HistoricalDataSet.SliceUpTo(symbol, asOfUtc)` 保证可见历史最多到当前 bar（`LookAheadBiasTests` 钉死） |
| **回测经纪商** | `BacktestBrokerService`（实现 `IBinanceUsdFutureService`）：记账口径与 Paper 券商一致，外加手续费/滑点记账与只增不减的成交日志 |
| **成交时机** | `SameBarClose`（默认，信号 bar 收盘价）或 `NextBarOpen`（下一 bar 开盘价——对"收盘价算信号"的因果诚实模式） |
| **成本** | `CommissionBps` + `SlippageBps`，逐条计入 `Trades[].CommissionUsd` 并汇总进 `Metrics.TotalCommissionUsd` |
| **指标** | `BacktestResult.Metrics`——CAGR / 夏普 / 卡尔玛 / 最大回撤（复用 `StrategyPerformanceAnalyzer`）+ 胜率 / 盈亏比 / 总手续费（交易层），由 `EquityCurve` + `Trades` 装配 |
| **参数扫描** | `ParameterSweepRunner`——每个网格点独立的 broker + 独立 DI 容器，`Parallel.ForEachAsync` 并行，结果按网格顺序落位 |
| **预热** | `WarmupBars` 前 N 根 bar 不交易，用于指标预热 |

**接入自己的宿主**（设计上离线——`Environment` 被强制为 `Paper`，无法误接实盘券商）：

```csharp
var services = new ServiceCollection();
services.AddQuantInfraNetBacktest(
    configureBacktest: b => { b.InitialEquityUsd = 10000m; b.WarmupBars = 20; },
    configureOrchestration: o =>
    {
        o.Parameters["Symbol"] = "AAPL";
        o.Parameters["Strategy"] = "MaCross";
    });

using var provider = services.BuildServiceProvider();
var result = await provider.GetRequiredService<BacktestRunner>().RunAsync(myHistoricalDataSet, new[] { "AAPL" });
// result.EquityCurve、result.Trades、result.Metrics
```

内置 3 策略任选（`MaCross` / `MeanReversion` / `PairTradingZScore`，改一个 `Parameters.Strategy` 值），或传 `customSignalGenerator` 用自己的策略类——**同一个类**既跑回测也跑 Paper。真实数据只需把任意 `Ohlcv` 序列（一次性预取，绝不放在回放循环内）构建为 `HistoricalDataSet`。

完整契约与护栏（成交时机矩阵、里程碑 B0–B6、依赖白名单）见 [Trading Runtime Design](TradingRuntimeDesign.md)；分步使用说明见 [回测引擎详细使用说明](BacktestQuickStart-ch.md)。

---

## 为什么要用这个库？

### 量化开发中的痛点

在构建量化交易系统时，大多数开发者会遇到以下挑战：

| 挑战 | 没有此库时会怎样 | Quant.Infra.Net 如何解决 |
|-----------|----------------------------------|-------------------------------|
| **数据源分散** | 每个 API（Yahoo、Binance、Alpaca、Schwab）返回的数据格式各不相同 —— 你需要为每个供应商编写转换器 | 统一的 `ITraditionalFinanceSourceDataService` 和 `ICryptoSourceDataService`，标准化的 OHLCV 模型；新数据源只需实现同一接口 |
| **券商接入繁琐** | 连接 Binance 期货需要处理 API 密钥、速率限制、WebSocket 重连；Schwab 需要 OAuth 流程；IB 需要 TWS/Gateway IPC | 单一 `IBrokerService` 抽象 —— 更换券商只需改配置，不改动代码 |
| **重复造分析轮子** | 每次都要从头实现 ADF 检验、回归分析、Z-Score 标准化 | `IAnalysisService` 提供 10+ 种统计方法，开箱即用 |
| **缺少告警通道** | 策略静默运行 —— 等待数小时后才能发现结果 | 内置钉钉、企业微信和邮件通知，策略事件自动推送 |
| **绩效跟踪靠手工** | 计算 CAGR、夏普比率、最大回撤需要手动编写公式，容易出错 | `StrategyPerformanceAnalyzer` 实现了标准指标并经过单元测试；ScottPlot 集成用于图表绘制 |

### 适用人群

- 在 .NET 平台上构建策略的量化研究员和交易员
- 希望用单个 NuGet 包处理数据获取、执行和告警的开发者
- 需要在 Binance、Alpaca、Schwab 和 Interactive Brokers 之间保持统一券商抽象的团队
- 厌倦了每个新项目都要写相同集成代码的人

---

## 快速开始

### 第一步：通过 NuGet 安装

```bash
# 创建项目（或使用已有项目）
dotnet new console -n MyQuantApp
cd MyQuantApp

# 添加库
dotnet add package Quant.Infra.Net --version 1.5.1

# Python 数据源需要此包（Yahoo Finance 通过 yfinance）
dotnet add package pythonnet

# 推荐使用依赖注入
dotnet add package Microsoft.Extensions.DependencyInjection
```

### 第二步：代码中使用

```csharp
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Notification.Service;
using Microsoft.Extensions.DependencyInjection;

// 通过依赖注入注册服务
var services = new ServiceCollection();
services.AddQuantInfraNet();  // 注册所有模块

// --- 数据：从多源获取 OHLCV ---
var dataService = services.BuildServiceProvider()
    .GetService<ITraditionalFinanceService>();
var bars = await dataService.GetOhlcvListAsync("AAPL", DateTime.Now.AddDays(-30), DateTime.Now);

// --- 分析：配对交易相关性 & ADF 检验 ---
var analysis = services.BuildServiceProvider()
    .GetService<IAnalysisService>();
var correlation = await analysis.CalculateCorrelationAsync(aaplPrices, msftPrices);
var isStationary = await analysis.TestStationarityAsync(spreadSeries);

// --- 券商：跨平台下单 ---
var broker = services.BuildServiceProvider()
    .GetService<IBrokerService>();
var orderResult = await broker.PlaceOrderAsync(new OrderRequest { Symbol = "AAPL", Side = Side.Buy, Quantity = 10 });

// --- 组合：绩效分析 ---
var portfolio = services.BuildServiceProvider()
    .GetService<IPortfolioSnapshotService>();
var snapshot = await portfolio.GetSnapshotAsync(accountId);

// --- 通知：策略告警 ---
var dingTalk = services.BuildServiceProvider()
    .GetService<IDingtalkService>();
await dingTalk.SendStrategyAlert("AAPL/MSFT 价差均值回归触发");
```

### 第三步：配置

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

## 版本历史

| 版本 | 日期 | 描述 |
|---------|------|-------------|
| **1.5.2** *(当前)* | 2026-08-28 | **编排层 Orchestration Layer（Beta）** —— 新增 `Quant.Infra.Net.Orchestration` 包：`AddQuantInfraNetOrchestration()` DI 入口、8 阶段管道、3 个内置策略（PairTradingZScore/MaCross/MeanReversion）、默认 Paper（纯内存零网络）执行、含熔断的风控前置检查、按严重级别路由的通知、可直接运行的控制台 Demo。详见 [编排层设计文档](OrchestrationLayerDesign.md) |
| 1.5.1 | 2026-08-12 | CodeStandard.md 合规 —— 所有公共成员添加中英文 XML 文档、参数验证审计、版本号统一 |
| 1.5.0 | 2026-05-28 | **Interactive Brokers (InterReact)** 完整集成 —— 通过 TWS/Gateway 下单、行情数据、账户管理；**Charles Schwab** 完整券商服务 —— 报价、期权链、订单、持仓；许可证改为 MIT；增强分析服务单元测试 |
| 1.4.0 | 2024-05-16 | 更新 API 集成以应对近期券商变动，添加全面文档 |
| 1.3.0 | 2024-04-05 | 增强通知服务，支持邮件模板和改进的错误处理 |
| 1.2.0 | 2024-03-10 | 改善 Python 集成稳定性，添加新的统计分析方法 |
| 1.1.0 | 2024-02-20 | 添加 Schwab 券商集成支持，增强组合绩效指标 |
| 1.0.0 | 2024-01-15 | 初始发布，包含核心功能：数据获取、统计分析、交易执行和通知 |

---

## 代码规范

本项目遵循 [docs/CodeStandard.md](docs/CodeStandard.md) 中定义的编码规范：
- 所有公共成员必须带有中英文双语 XML 文档
- 遵循 SOLID 设计原则
- 所有入口点都有参数验证
- UTC 时间处理和一致的枚举管理

---

## 测试注意事项

> ⚠️ **Crypto 交易所地区合规说明**：不同国家和地区对 Crypto 交易所的监管要求各不相同。例如中国、美国 IP 不能访问 Binance API，但新加坡可以。请遵守当地法律法规，本 Repo 仅提供技术方案，您为自己的行为负有全部责任。
>
> ```bash
> dotnet test --filter "FullyQualifiedName!~Binance"
> ```

---

## 生态系统

| 项目 | 描述 |
|---------|-------------|
| **Quant.Infra.Net** (本仓库) | 核心量化交易库 —— 数据、分析、执行、通知 |
| [**Quant.Infra.Net.Pro**](https://github.com/memoryfraction/Quant.Infra.Net.Pro) | 生产级 Charles Schwab Web 应用，支持无人值守 OAuth 令牌管理和完整仪表板 |

---

## 许可证

[MIT](LICENSE) — © 2024–2026 Rong (Rex) Fan

> **免责声明**：详见 [免责声明](docs/DISCLAIMER.md) 了解完整免责条款与责任限制。
