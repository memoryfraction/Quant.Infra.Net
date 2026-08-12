# Quant.Infra.Net

[![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)  [![Version](https://img.shields.io/badge/Version-1.5.1-blue.svg)](https://github.com/memoryfraction/Quant.Infra.Net/releases)  [![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **Quant.Infra.Net** 是一个 .NET 量化交易基础库 —— 数据获取、统计分析、券商集成、组合分析和通知推送，一个 NuGet 包全部搞定。

> 📖 [文档 / GitHub Pages](https://memoryfraction.github.io/Quant.Infra.Net/)

---

## 这是什么？

Quant.Infra.Net 提供统一的 C# API，将连接多个金融数据源、券商和通知渠道的复杂性封装起来。你不再需要为每个平台编写单独的集成代码，而是获得一致的接口和开箱即用的实现。

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
| **1.5.1** *(当前)* | 2026-08-12 | CodeStandard.md 合规 —— 所有公共成员添加中英文 XML 文档、参数验证审计、版本号统一 |
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

> ⚠️ **Binance 单元测试**：Binance 集成测试需要新加坡 IP 地址才能通过。从中国或美国运行时，由于 Binance API 端口的地区访问限制，这些测试会失败。运行以下命令排除 Binance 测试以测试其他模块：
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
