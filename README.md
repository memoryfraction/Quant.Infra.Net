# Quant.Infra.Net

[![NuGet](https://img.shields.io/nuget/v/Quant.Infra.Net?color=blue)](https://www.nuget.org/packages/Quant.Infra.Net)
[![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)

> **Quant.Infra.Net** 是一个面向 .NET 的量化交易基础设施类库，帮助你用最少的代码完成数据获取、统计分析、交易执行和消息通知。
>
> **Quant.Infra.Net** is a .NET quantitative trading infrastructure library that lets you fetch data, run statistical analysis, execute trades, and send notifications with minimal code.

---

- [中文版](#中文版)
- [English](#english)

---

# 中文版

## 为什么使用本库

在量化交易开发中，你可能反复遇到这些问题：

| 你遇到的问题 | 本库提供的方案 |
|---|---|
| 股票 / 加密货币数据接口分散、格式不统一 | 统一的 `ITraditionalFinanceSourceDataService` 和 `ICryptoSourceDataService`，标准化 OHLCV 模型 |
| 配对交易需要 ADF 检验、OLS 回归、Z-Score、相关性分析 | `IAnalysisService` 提供现成方法，一行代码调用 |
| 对接 Binance、Alpaca 等券商时重复造轮子 | `IBinanceUsdFutureService`、`IUSEquityBrokerService` 等统一抽象 |
| 策略运行后缺少通知链路 | 钉钉 `IDingtalkService`、企业微信 `IWeChatService`、邮件 `EmailService` |
| 评估 CAGR、Sharpe、Calmar、最大回撤要自己写 | `StrategyPerformanceAnalyzer` 内置全部指标 |
| 定时器、滚动窗口每次都重写 | `IntervalTrigger`、`RollingWindow<T>` 开箱即用 |

简而言之：**不重复造轮子，专注策略本身。**

## 能解决什么问题

1. **数据获取** — 从 Yahoo Finance 下载股票日线，从 Binance 批量下载加密货币 K 线，从 CSV/MySQL/MongoDB 读取本地历史数据。
2. **统计分析** — 相关性、ADF 平稳性检验、OLS 线性回归、Z-Score、Shapiro-Wilk 正态性检验、配对交易价差计算。
3. **交易执行** — Binance 合约下单 / 清仓、Alpaca 美股下单 / 清仓，支持 Testnet / Paper / Live 环境切换。
4. **通知推送** — 钉钉群机器人、企业微信 Webhook、个人邮箱 / 商业邮件批量发送。
5. **组合与绩效** — 投资组合快照、净值曲线绘图、CAGR / Sharpe / Calmar / 最大回撤计算。
6. **工具类** — 滚动窗口、定时触发器、分辨率转换、DataFrame 读写。

## 快速开始

### 第一步：安装

```bash
dotnet new console -n MyQuantApp
cd MyQuantApp
dotnet add package Quant.Infra.Net
```

如果你在仓库内开发，也可以用 ProjectReference：

```xml
<ProjectReference Include="..\Quant.Infra.Net\Quant.Infra.Net.csproj" />
```

### 第二步：复制以下 Program.cs 直接运行（无需 API Key）

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Shared.Model;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. 注册服务
        var services = new ServiceCollection();
        services.AddScoped<ITraditionalFinanceSourceDataService, TraditionalFinanceSourceDataService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        var provider = services.BuildServiceProvider();

        var dataService = provider.GetRequiredService<ITraditionalFinanceSourceDataService>();
        var analysis   = provider.GetRequiredService<IAnalysisService>();

        // 2. 下载 AAPL 和 MSFT 最近一年日线（Yahoo Finance，无需 Key）
        var end   = DateTime.UtcNow;
        var start = end.AddYears(-1);

        Console.WriteLine("正在下载 AAPL 日线数据...");
        var aapl = await dataService.DownloadOhlcvListAsync("AAPL", start, end, ResolutionLevel.Daily);
        Console.WriteLine($"AAPL 行数: {aapl?.Count ?? 0}");

        Console.WriteLine("正在下载 MSFT 日线数据...");
        var msft = await dataService.DownloadOhlcvListAsync("MSFT", start, end, ResolutionLevel.Daily);
        Console.WriteLine($"MSFT 行数: {msft?.Count ?? 0}");

        // 3. 计算收盘价相关性
        if (aapl?.Count > 10 && msft?.Count > 10)
        {
            var aaplClose = aapl.Select(x => (double)x.Close).ToList();
            var msftClose = msft.Select(x => (double)x.Close).ToList();

            double corr = analysis.CalculateCorrelation(aaplClose, msftClose);
            Console.WriteLine($"AAPL vs MSFT 收盘价相关性: {corr:F4}");

            // 4. OLS 回归
            var (slope, intercept) = analysis.PerformOLSRegression(aaplClose, msftClose);
            Console.WriteLine($"OLS 回归: Slope={slope:F4}, Intercept={intercept:F4}");

            // 5. 计算价差并做 ADF 平稳性检验
            var spread = msftClose
                .Zip(aaplClose, (m, a) => m - slope * a - intercept)
                .ToList();

            bool isStationary = analysis.AugmentedDickeyFullerTest(spread);
            Console.WriteLine($"价差 ADF 检验（平稳）: {isStationary}");

            // 6. 计算最新 Z-Score
            double zScore = analysis.CalculateZScores(spread, spread.Last());
            Console.WriteLine($"最新 Z-Score: {zScore:F4}");
        }
    }
}
```

运行：

```bash
dotnet run
```

预期输出示例：

```
正在下载 AAPL 日线数据...
AAPL 行数: 252
正在下载 MSFT 日线数据...
MSFT 行数: 252
AAPL vs MSFT 收盘价相关性: 0.9213
OLS 回归: Slope=1.8472, Intercept=23.5610
价差 ADF 检验（平稳）: True
最新 Z-Score: -0.3172
```

## 更多使用场景

### 场景 1：获取 S&P 500 成分股列表

```csharp
var dataService = provider.GetRequiredService<ITraditionalFinanceSourceDataService>();
var symbols = await dataService.GetSp500SymbolsAsync();
Console.WriteLine($"S&P 500 成分股数量: {symbols.Count()}");
```

### 场景 2：Binance 合约交易（需要 API Key）

```csharp
var futureService = provider.GetRequiredService<IBinanceUsdFutureService>();
futureService.ExchangeEnvironment = ExchangeEnvironment.Testnet; // 先用测试网

// 查询余额
decimal balance = await futureService.GetusdFutureAccountBalanceAsync();
Console.WriteLine($"账户余额: {balance}");

// 按比例建仓
await futureService.SetUsdFutureHoldingsAsync("BTCUSDT", 0.10, PositionSide.Long);

// 查看持仓
var positions = await futureService.GetHoldingPositionAsync();
Console.WriteLine($"持仓数量: {positions.Count()}");

// 清仓
await futureService.LiquidateUsdFutureAsync("BTCUSDT");
```

### 场景 3：Alpaca 美股交易（需要 API Key）

```csharp
var broker = provider.GetRequiredService<IUSEquityBrokerService>();
broker.ExchangeEnvironment = ExchangeEnvironment.Paper; // 模拟盘

decimal equity = await broker.GetAccountEquityAsync();
Console.WriteLine($"账户权益: {equity}");

await broker.SetHoldingsAsync("AAPL", 0.05m);  // 5% 仓位
await broker.LiquidateAsync("AAPL");            // 清仓
```

### 场景 4：钉钉 / 企业微信通知

```csharp
// 钉钉
var dingtalk = provider.GetRequiredService<IDingtalkService>();
await dingtalk.SendNotificationAsync("策略信号：买入 AAPL", accessToken, secret);

// 企业微信
var wechat = provider.GetRequiredService<IWeChatService>();
await wechat.SendTextNotificationAsync("订单已成交", webHookUrl);
```

### 场景 5：统计分析全流程

```csharp
var analysis = provider.GetRequiredService<IAnalysisService>();

// 相关性
double corr = analysis.CalculateCorrelation(seriesA, seriesB);

// OLS 回归：diff = B - Slope * A - Intercept
var (slope, intercept) = analysis.PerformOLSRegression(seriesA, seriesB);

// ADF 平稳性检验（返回 bool）
bool isStationary = analysis.AugmentedDickeyFullerTest(spread);

// ADF 平稳性检验（返回详细结果，需要 Python 环境）
AdfTestResult adfResult = analysis.AugmentedDickeyFullerTestPython(spread);

// Z-Score
double z = analysis.CalculateZScores(spread, currentValue);

// Shapiro-Wilk 正态性检验
bool isNormal = analysis.PerformShapiroWilkTest(spread);
```

### 场景 6：滚动窗口与定时触发器

```csharp
// 滚动窗口：保持最近 20 根 K 线
var window = new RollingWindow<double>(20);
window.Add(100.5);
window.Add(101.2);
// ... 持续添加
if (window.IsReady)
{
    Console.WriteLine($"窗口已满，共 {window.Count} 个元素");
}

// 定时触发器：每小时整点前 1 分钟触发
var trigger = new IntervalTrigger(StartMode.NextHour, TimeSpan.FromMinutes(-1));
trigger.IntervalTriggered += (sender, e) =>
{
    Console.WriteLine($"触发! {DateTime.UtcNow}");
};
trigger.Start();
```

### 场景 7：投资组合绩效分析

```csharp
// marketValueDict: Dictionary<DateTime, decimal> — 每日净值
double cagr       = StrategyPerformanceAnalyzer.CalculateCAGR(marketValueDict);
double sharpe     = StrategyPerformanceAnalyzer.CalculateSharpeRatio(marketValueDict, riskFreeRate);
double calmar     = StrategyPerformanceAnalyzer.CalculateCalmarRatio(marketValueDict);
double maxDD      = StrategyPerformanceAnalyzer.CalculateMaximumDrawdown(values);

Console.WriteLine($"CAGR={cagr:P2}, Sharpe={sharpe:F2}, Calmar={calmar:F2}, MaxDD={maxDD:P2}");
```

## 如何运行单元测试

```bash
cd src
dotnet restore
dotnet build
dotnet test
```

运行指定测试类：

```bash
dotnet test --filter "FullyQualifiedName~AnalysisServiceTests"
```

## 项目结构

```
Quant.Infra.Net/
├── Analysis/           # 统计分析：ADF、OLS、相关性、Z-Score、配对交易
├── Broker/             # 券商接入：Binance、Alpaca、InteractiveBrokers
├── Notification/       # 通知：钉钉、企业微信、邮件
├── Order/              # 订单模型
├── Portfolio/          # 投资组合快照、绩效分析、净值曲线
├── Shared/             # 公共模型、枚举、RollingWindow、IntervalTrigger、UtilityService
└── SourceData/         # 数据采集：Yahoo Finance、Binance、CSV、MySQL、MongoDB
```

## 注意事项

- **ADF Python 模式**：`AugmentedDickeyFullerTestPython` 方法依赖本机 Python 环境，需安装 `numpy`、`pandas`、`statsmodels`。如果没有 Python 环境，请使用 `AugmentedDickeyFullerTest`（纯 .NET 实现）。
- **API Key 配置**：Binance / Alpaca 等实盘接口需要在 `appsettings.json` 或 User Secrets 中配置 API Key 和 Secret。
- **ExchangeEnvironment**：支持 `Testnet`（测试网）、`Paper`（模拟盘）、`Live`（实盘）三种环境，建议开发阶段使用 Testnet 或 Paper。
- **Binance IP 限制**：Binance API 对部分国家/地区的 IP 存在访问限制，如果你在运行 Binance 相关单元测试时遇到连接错误，这并非代码问题，请查阅 [Binance 官方文档](https://www.binance.com/en/support) 了解受限地区列表。
- **合规免责声明**：本项目仅提供量化交易基础设施工具，不构成任何投资建议。用户需自行确保使用本库时符合所在国家/地区的法律法规及交易所合规要求，因使用本库产生的任何法律或财务后果由用户自行承担。

**Quant.Infra.Net** is a professional quantitative trading infrastructure framework built on **.NET 8**. It is designed to provide a robust foundation for quant developers, covering market data ingestion, advanced statistical analysis, order execution, and automated monitoring.

---

# English

## Why Use This Library

When developing quantitative trading systems, you likely face these recurring problems:

| Problem | What this library provides |
|---|---|
| Stock / crypto data APIs are scattered with inconsistent formats | Unified `ITraditionalFinanceSourceDataService` and `ICryptoSourceDataService` with standardized OHLCV models |
| Pair trading requires ADF test, OLS regression, Z-Score, correlation | `IAnalysisService` provides ready-to-use methods in one line |
| Integrating Binance, Alpaca, etc. means rewriting boilerplate | `IBinanceUsdFutureService`, `IUSEquityBrokerService` unified abstractions |
| No notification pipeline after strategy runs | DingTalk `IDingtalkService`, WeChat `IWeChatService`, Email `EmailService` |
| CAGR, Sharpe, Calmar, max drawdown must be coded from scratch | `StrategyPerformanceAnalyzer` with all metrics built in |
| Timers and rolling windows rewritten every project | `IntervalTrigger`, `RollingWindow<T>` ready to use |

In short: **stop reinventing the wheel — focus on your strategy.**

## What Problems It Solves

1. **Data Acquisition** — Download stock daily bars from Yahoo Finance, batch-download crypto klines from Binance, read local data from CSV/MySQL/MongoDB.
2. **Statistical Analysis** — Correlation, ADF stationarity test, OLS regression, Z-Score, Shapiro-Wilk normality test, pair-trading spread calculation.
3. **Trade Execution** — Binance futures order/liquidate, Alpaca US equity order/liquidate, with Testnet/Paper/Live environment switching.
4. **Notifications** — DingTalk bot, WeChat Work webhook, personal/commercial bulk email.
5. **Portfolio & Performance** — Portfolio snapshots, equity curve charting, CAGR/Sharpe/Calmar/MaxDrawdown calculation.
6. **Utilities** — Rolling window, interval trigger, resolution conversion, DataFrame I/O.

## Quick Start

### Step 1: Install

```bash
dotnet new console -n MyQuantApp
cd MyQuantApp
dotnet add package Quant.Infra.Net
```

Or use a ProjectReference when developing inside the repo:

```xml
<ProjectReference Include="..\Quant.Infra.Net\Quant.Infra.Net.csproj" />
```

### Step 2: Copy This Program.cs and Run (No API Key Needed)

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Shared.Model;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Register services
        var services = new ServiceCollection();
        services.AddScoped<ITraditionalFinanceSourceDataService, TraditionalFinanceSourceDataService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        var provider = services.BuildServiceProvider();

        var dataService = provider.GetRequiredService<ITraditionalFinanceSourceDataService>();
        var analysis   = provider.GetRequiredService<IAnalysisService>();

        // 2. Download AAPL & MSFT 1-year daily bars (Yahoo Finance, no key needed)
        var end   = DateTime.UtcNow;
        var start = end.AddYears(-1);

        Console.WriteLine("Downloading AAPL daily OHLCV...");
        var aapl = await dataService.DownloadOhlcvListAsync("AAPL", start, end, ResolutionLevel.Daily);
        Console.WriteLine($"AAPL rows: {aapl?.Count ?? 0}");

        Console.WriteLine("Downloading MSFT daily OHLCV...");
        var msft = await dataService.DownloadOhlcvListAsync("MSFT", start, end, ResolutionLevel.Daily);
        Console.WriteLine($"MSFT rows: {msft?.Count ?? 0}");

        // 3. Compute close-price correlation
        if (aapl?.Count > 10 && msft?.Count > 10)
        {
            var aaplClose = aapl.Select(x => (double)x.Close).ToList();
            var msftClose = msft.Select(x => (double)x.Close).ToList();

            double corr = analysis.CalculateCorrelation(aaplClose, msftClose);
            Console.WriteLine($"AAPL vs MSFT correlation: {corr:F4}");

            // 4. OLS regression
            var (slope, intercept) = analysis.PerformOLSRegression(aaplClose, msftClose);
            Console.WriteLine($"OLS: Slope={slope:F4}, Intercept={intercept:F4}");

            // 5. Compute spread and run ADF stationarity test
            var spread = msftClose
                .Zip(aaplClose, (m, a) => m - slope * a - intercept)
                .ToList();

            bool isStationary = analysis.AugmentedDickeyFullerTest(spread);
            Console.WriteLine($"Spread ADF stationary: {isStationary}");

            // 6. Latest Z-Score
            double zScore = analysis.CalculateZScores(spread, spread.Last());
            Console.WriteLine($"Latest Z-Score: {zScore:F4}");
        }
    }
}
```

Run:

```bash
dotnet run
```

Expected output:

```
Downloading AAPL daily OHLCV...
AAPL rows: 252
Downloading MSFT daily OHLCV...
MSFT rows: 252
AAPL vs MSFT correlation: 0.9213
OLS: Slope=1.8472, Intercept=23.5610
Spread ADF stationary: True
Latest Z-Score: -0.3172
```

## More Usage Scenarios

### Scenario 1: Get S&P 500 Constituent Symbols

```csharp
var dataService = provider.GetRequiredService<ITraditionalFinanceSourceDataService>();
var symbols = await dataService.GetSp500SymbolsAsync();
Console.WriteLine($"S&P 500 count: {symbols.Count()}");
```

### Scenario 2: Binance Futures Trading (API Key Required)

```csharp
var futureService = provider.GetRequiredService<IBinanceUsdFutureService>();
futureService.ExchangeEnvironment = ExchangeEnvironment.Testnet;

decimal balance = await futureService.GetusdFutureAccountBalanceAsync();
Console.WriteLine($"Balance: {balance}");

await futureService.SetUsdFutureHoldingsAsync("BTCUSDT", 0.10, PositionSide.Long);

var positions = await futureService.GetHoldingPositionAsync();
Console.WriteLine($"Positions: {positions.Count()}");

await futureService.LiquidateUsdFutureAsync("BTCUSDT");
```

### Scenario 3: Alpaca US Equity Trading (API Key Required)

```csharp
var broker = provider.GetRequiredService<IUSEquityBrokerService>();
broker.ExchangeEnvironment = ExchangeEnvironment.Paper;

decimal equity = await broker.GetAccountEquityAsync();
Console.WriteLine($"Account equity: {equity}");

await broker.SetHoldingsAsync("AAPL", 0.05m);
await broker.LiquidateAsync("AAPL");
```

### Scenario 4: DingTalk / WeChat Notifications

```csharp
var dingtalk = provider.GetRequiredService<IDingtalkService>();
await dingtalk.SendNotificationAsync("Signal: Buy AAPL", accessToken, secret);

var wechat = provider.GetRequiredService<IWeChatService>();
await wechat.SendTextNotificationAsync("Order filled", webHookUrl);
```

### Scenario 5: Full Statistical Analysis Pipeline

```csharp
var analysis = provider.GetRequiredService<IAnalysisService>();

double corr = analysis.CalculateCorrelation(seriesA, seriesB);

var (slope, intercept) = analysis.PerformOLSRegression(seriesA, seriesB);

bool isStationary = analysis.AugmentedDickeyFullerTest(spread);

AdfTestResult adfResult = analysis.AugmentedDickeyFullerTestPython(spread);

double z = analysis.CalculateZScores(spread, currentValue);

bool isNormal = analysis.PerformShapiroWilkTest(spread);
```

### Scenario 6: Rolling Window & Interval Trigger

```csharp
var window = new RollingWindow<double>(20);
window.Add(100.5);
window.Add(101.2);
if (window.IsReady)
    Console.WriteLine($"Window full, count={window.Count}");

var trigger = new IntervalTrigger(StartMode.NextHour, TimeSpan.FromMinutes(-1));
trigger.IntervalTriggered += (sender, e) =>
{
    Console.WriteLine($"Triggered at {DateTime.UtcNow}");
};
trigger.Start();
```

### Scenario 7: Portfolio Performance Analysis

```csharp
double cagr   = StrategyPerformanceAnalyzer.CalculateCAGR(marketValueDict);
double sharpe  = StrategyPerformanceAnalyzer.CalculateSharpeRatio(marketValueDict, riskFreeRate);
double calmar  = StrategyPerformanceAnalyzer.CalculateCalmarRatio(marketValueDict);
double maxDD   = StrategyPerformanceAnalyzer.CalculateMaximumDrawdown(values);

Console.WriteLine($"CAGR={cagr:P2}, Sharpe={sharpe:F2}, Calmar={calmar:F2}, MaxDD={maxDD:P2}");
```

## Running Unit Tests

```bash
cd src
dotnet restore
dotnet build
dotnet test
```

Run a specific test class:

```bash
dotnet test --filter "FullyQualifiedName~AnalysisServiceTests"
```

## Project Structure

```
Quant.Infra.Net/
├── Analysis/           # Statistical analysis: ADF, OLS, correlation, Z-Score, pair trading
├── Broker/             # Broker integration: Binance, Alpaca, InteractiveBrokers
├── Notification/       # Notifications: DingTalk, WeChat Work, Email
├── Order/              # Order models
├── Portfolio/          # Portfolio snapshots, performance analysis, equity curves
├── Shared/             # Common models, enums, RollingWindow, IntervalTrigger, UtilityService
└── SourceData/         # Data: Yahoo Finance, Binance, CSV, MySQL, MongoDB
```

## Notes

- **ADF Python mode**: `AugmentedDickeyFullerTestPython` requires a local Python environment with `numpy`, `pandas`, and `statsmodels`. If Python is not available, use `AugmentedDickeyFullerTest` (pure .NET implementation).
- **API Key configuration**: Binance / Alpaca live trading requires API Key and Secret in `appsettings.json` or User Secrets.
- **ExchangeEnvironment**: Supports `Testnet`, `Paper`, and `Live`. Use Testnet or Paper during development.
- **Binance IP Restrictions**: The Binance API restricts access from certain countries/regions. If you encounter connection errors when running Binance-related unit tests, this is not a code issue. Please refer to the [Binance official documentation](https://www.binance.com/en/support) for the list of restricted regions.
- **Compliance Disclaimer**: This project provides quantitative trading infrastructure tools only and does not constitute investment advice. Users are solely responsible for ensuring compliance with all applicable laws, regulations, and exchange rules in their jurisdiction. The authors assume no liability for any legal or financial consequences arising from the use of this library.

## License

See [LICENSE](LICENSE) for details.
