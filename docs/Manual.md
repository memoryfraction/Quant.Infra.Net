# Quant.Infra.Net User Manual / 用户使用手册

> **Target audience / 目标读者**: Quantitative developers who want to focus on strategy logic, not infrastructure plumbing. Focuses on **"how to use"** rather than internal implementation details.

---

## Table of Contents / 目录

1. [Getting Started](#1-getting-started)
2. [Configuration Guide](#2-configuration-guide)
3. [Data Source Module](#3-data-source-module)
4. [Broker and Order Module](#4-broker-and-order-module)
5. [Analysis Module](#5-analysis-module)
6. [Notification Module](#6-notification-module)
7. [Portfolio Module](#7-portfolio-module)
8. [Common Usage Patterns](#8-common-usage-patterns)
9. [Troubleshooting and FAQ](#9-troubleshooting-and-faq)
10. [Appendix — Resolution Levels](#appendix-resolution-levels)

---

## 1. Getting Started

### 1.1 Prerequisites / 前置条件

| Requirement | Minimum Version | Notes / 说明 |
|-------------|-----------------|-------------|
| .NET SDK | 8.0 LTS | Required for building and running projects that reference this library |
| Python 3.x (optional) | 3.8+ | Needed only if using Yahoo Finance data source (`pythonnet` bridge). Can use `conda` or system Python. |

### 1.2 Installation / 安装

```bash
# Create a new .NET console project
dotnet new console -n MyQuantApp
cd MyQuantApp

# Install the library from NuGet (use latest stable version)
dotnet add package Quant.Infra.Net --version 1.5.1

# If using Yahoo Finance data source (Python bridge)
dotnet add package pythonnet

# For DI container support (recommended)
dotnet add package Microsoft.Extensions.DependencyInjection
```

### 1.3 First Lines of Code / 第一段代码

```csharp
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static async Task Main()
    {
        // Step 1: Register all Quant.Infra.Net modules at once
        var services = new ServiceCollection();
        services.AddQuantInfraNet();

        // Step 2: Build the service provider (creates all singleton/Scoped instances)
        var provider = services.BuildServiceProvider();

        // Step 3: Resolve any service by its interface — no manual instantiation needed
        var dataService = provider.GetService<ITraditionalFinanceSourceDataService>();

        // Step 4: Fetch AAPL daily data for the past 30 days
        var bars = await dataService.DownloadOhlcvListAsync(
            "AAPL",
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            ResolutionLevel.Daily);

        Console.WriteLine($"Downloaded {bars.OhlcvSet.Count} OHLCV bars for AAPL");
    }
}
```

> **Key concept**: All services are registered via Dependency Injection. You never `new` a service directly — always resolve through the `IServiceProvider`. This allows swapping implementations (e.g., testnet → live) without changing strategy code.

---

## 2. Configuration Guide

### 2.1 appsettings.json / 配置文件

Create an `appsettings.json` in your project root. Below is a complete reference configuration with all supported sections:

```json
{
  "BinanceApi": {
    "ApiKey": "",
    "SecretKey": "",
    "Environment": "testnet"
  },
  "YahooFinance": {
    "PythonPath": "C:\\Users\\you\\Anaconda3\\python.exe"
  },
  "DingTalk": {
    "AccessToken": "",
    "Secret": ""
  },
  "WeChat": {
    "WebHook": ""
  },
  "Email": {
    "Personal": {
      "SmtpServer": "smtp.gmail.com",
      "Port": 587,
      "SenderEmail": "you@gmail.com"
    }
  },
  "AlpacaApi": {
    "ApiKey": "",
    "ApiSecret": ""
  },
  "SchwabApi": {
    "ApiKey": "",
    "ApiSecret": "",
    "AccountId": ""
  },
  "InterReactOptions": {
    "ServerIp": "127.0.0.1",
    "Port": 44659,
    "MaxRequestsPerSecond": 100
  }
}
```

### 2.2 Sensitive Settings — User Secrets / 敏感配置管理

Never commit API keys to source control. Use .NET User Secrets for development:

```bash
# Binance credentials
dotnet user-secrets set "BinanceApi:ApiKey" "your-api-key"
dotnet user-secrets set "BinanceApi:SecretKey" "your-secret-key"
dotnet user-secrets set "BinanceApi:Environment" "testnet"

# DingTalk credentials
dotnet user-secrets set "DingTalk:AccessToken" "your-token"
dotnet user-secrets set "DingTalk:Secret" "your-signing-secret"

# Alpaca credentials
dotnet user-secrets set "AlpacaApi:ApiKey" "your-key"
dotnet user-secrets set "AlpacaApi:ApiSecret" "your-secret"
```

### 2.3 Exchange Environment Switching / 交易环境切换

All broker services support switching between environments:

| Broker | Test Environment | Live Environment | How to switch |
|--------|-----------------|------------------|---------------|
| Binance Futures | `testnet` | `live` | Set `"BinanceApi:Environment": "testnet"` in config, or set `ExchangeEnvironment` property at runtime |
| Alpaca (US Equity) | Paper trading account | Live account | Configured via API key pointing to paper/live account |
| Charles Schwab | Account with limited access | Full account | Same — the API distinguishes by account type |

```csharp
// Runtime environment switch example for Binance Futures
var binance = provider.GetService<IBinanceUsdFutureService>();
binance.ExchangeEnvironment = ExchangeEnvironment.Testnet;  // switch to testnet
// ... later, when ready for production:
binance.ExchangeEnvironment = ExchangeEnvironment.Live;     // switch to live
```

> ⚠️ **Always start with testnet/paper trading**. Place no real-money orders until you have verified your strategy logic on simulation data.

---

## 3. Data Source Module

### 3.1 Yahoo Finance — Traditional Market Data / 传统行情数据

**Service**: `ITraditionalFinanceSourceDataService`

#### Download OHLCV Data / 下载行情数据

```csharp
var dataService = provider.GetService<ITraditionalFinanceSourceDataService>();

// Fetch daily bars for a single symbol
var result = await dataService.DownloadOhlcvListAsync(
    symbol: "AAPL",
    startDt: DateTime.UtcNow.AddMonths(-1),
    endDt: DateTime.UtcNow,
    period: ResolutionLevel.Daily);  // Daily | Hourly | Minute

Console.WriteLine($"Downloaded {result.OhlcvSet.Count} bars");
```

#### Get S&P 500 Symbols / 获取标普500成分股

```csharp
// Get all 500 symbols (default)
var allSymbols = await dataService.GetSp500SymbolsAsync(500);

// Or get a subset for quick testing
var top10 = await dataService.GetSp500SymbolsAsync(10);
```

#### Save and Load from Local CSV / 本地CSV持久化

The library supports reading/writing OHLCV data as CSV files. This is useful for caching or offline backtesting:

```csharp
// Download and persist to disk
var bars = await dataService.BeginSyncSourceDailyDataAsync(
    "MSFT",
    DateTime.UtcNow.AddMonths(-1),
    DateTime.UtcNow,
    fullPathFileName: @"C:\data\MSFT_daily.csv");

// Later — read from the cached file without re-downloading
var cachedBars = new IOService().ReadCsv(@"C:\data\MSFT_daily.csv");
```

> **CSV format**: Each row contains `DateTime, Open, High, Low, Close, Volume`. Files are auto-created including parent directories.

### 3.2 Binance — Crypto Data / 加密货币数据

Binance data is accessible through two interfaces depending on the market:

#### Binance Spot Market / 现货市场

**Service**: `IBinanceSpotService`

```csharp
var spotService = provider.GetService<IBinanceSpotService>();

// Get all available spot trading pairs
var symbols = await spotService.GetSpotSymbolsAsync();

// Fetch hourly OHLCV for BTC/USDT
var bars = await spotService.GetOhlcvListAsync(
    "BTCUSDT",
    DateTime.UtcNow.AddDays(-7),
    DateTime.UtcNow,
    ResolutionLevel.Hourly);
```

#### Binance USD-M Futures / USDT永续合约

**Service**: `IBinanceUsdFutureService`

```csharp
var futuresService = provider.GetService<IBinanceUsdFutureService>();

// Get all available futures symbols
var symbols = await futuresService.GetUsdFutureSymbolsAsync();

// Fetch OHLCV data for ETH/USDT perpetual futures
var bars = await futuresService.GetOhlcvListAsync(
    "ETHUSDT",
    DateTime.UtcNow.AddDays(-7),
    DateTime.UtcNow,
    ResolutionLevel.Hourly);
```

> **Region notice**: Binance API may be inaccessible from China or the US mainland. Use a Singapore IP address or VPN for testing. See [Testing Notes](#9-troubleshooting-and-faq) for more.

---

## 4. Broker and Order Module

### 4.1 Binance USD-M Futures / 币安USD-M期货

**Service**: `IBinanceUsdFutureService`

#### Check Position Status / 查看持仓状态

```csharp
var futures = provider.GetService<IBinanceUsdFutureService>();

// Check if a symbol has an open position
bool hasPosition = await futures.HasUsdFuturePositionAsync("BTCUSDT");

// Get all current positions (with quantity, entry price, unrealized P&L)
var positions = await futures.GetHoldingPositionAsync();
foreach (var pos in positions)
{
    Console.WriteLine($"{pos.Symbol}: {pos.PositionAmount} @ {pos.EntryPrice}");
}
```

#### Set Target Holdings / 设置目标仓位

Automatically adjusts your position to reach a target portfolio percentage:

```csharp
// Set BTCUSDT long position to 5% of total portfolio value
await futures.SetUsdFutureHoldingsAsync(
    symbol: "BTCUSDT",
    rate: 0.05,          // 5% of portfolio
    positionSide: PositionSide.Long);

// For short side:
await futures.SetUsdFutureHoldingsAsync("ETHUSDT", 0.03, PositionSide.Short);
```

#### Liquidate a Position / 清仓

```csharp
// Liquidate a single symbol
await futures.LiquidateUsdFutureAsync("BTCUSDT");
```

#### Account Balance and P&L / 账户余额和盈亏

```csharp
decimal balance = await futures.GetusdFutureAccountBalanceAsync();
double unrealizedProfitRate = await futures.GetusdFutureUnrealizedProfitRateAsync();

Console.WriteLine($"Balance: {balance:C} | Unrealized P&L Rate: {unrealizedProfitRate:P2}");
```

#### Position Mode — Hedge vs One-Way / 持仓模式

```csharp
// Check current mode
await futures.ShowPositionModeAsync();

// Switch to Hedge (dual-direction) mode — recommended for quantitative strategies
await futures.SetPositionModeAsync(isHedgeMode: true);

// Or switch to One-Way (single-direction) mode
await futures.SetPositionModeAsync(isHedgeMode: false);
```

### 4.2 Binance Order Service / 币安订单服务

**Service**: `IBinanceOrderService`

Provides granular order management for both Spot and Futures markets:

#### Place a Spot Order / 现货下单

```csharp
var orderService = provider.GetService<IBinanceOrderService>();

// Buy BTC with USDT (market order, by quantity)
var result = await orderService.PlaceSpotOrderAsync(
    symbol: "BTCUSDT",
    orderSide: OrderSide.Buy,
    spotOrderType: OrderActionType.Market,
    quantity: 0.01m);

// Sell BTC with limit order
await orderService.PlaceSpotOrderAsync(
    "BTCUSDT", OrderSide.Sell, OrderActionType.Limit,
    quantity: 0.01m, price: 50000m);
```

#### Place a Futures Order / 期货下单

```csharp
// Open a long position on BTC/USDT perpetual futures (market order)
await orderService.PlaceUsdFutureOrderAsync(
    symbol: "BTCUSDT",
    orderSide: OrderSide.Buy,           // Buy to open long
    quantity: 0.01m,                    // Always positive
    positionSide: PositionSide.Long,    // Long side
    orderType: FuturesOrderType.Market  // Market | Limit | StopMarket etc.
);

// Open a short position (opposite direction)
await orderService.PlaceUsdFutureOrderAsync(
    "ETHUSDT", OrderSide.Sell, 0.1m, PositionSide.Short
);
```

#### Manage Orders / 订单管理

```csharp
// Get all open orders for a specific symbol
var openOrders = await orderService.GetAllSpotOpenOrdersAsync("BTCUSDT");

// Cancel a specific order by ID
await orderService.CancelSpotOrderAsync("BTCUSDT", orderId: 12345L);

// Cancel ALL open orders for a symbol (emergency!)
await orderService.CancelAllOrdersAsync("BTCUSDT");

// Liquidate ALL positions across all symbols (emergency close-all)
await orderService.LiquidateAsync();
```

### 4.3 Charles Schwab / 嘉信理财

**Service**: `ISchwabBrokerService`

#### Get Real-time Quotes / 获取实时报价

```csharp
var schwab = provider.GetService<ISchwabBrokerService>();

// Single quote
var quote = await schwab.GetQuoteAsync("AAPL");
Console.WriteLine($"AAPL: {quote.Price} ({quote.Change:P2})");

// Batch quotes
var symbols = new List<string> { "AAPL", "MSFT", "GOOGL" };
var quotes = await schwab.GetQuotesAsync(symbols);
```

#### Place an Order / 下单

```csharp
// Market buy order
var orderId = await schwab.PlaceOrderAsync(new SchwabOrderRequest
{
    OrderType = "market",
    Side = "buy",
    Qty = 10,
    Symbol = "MSFT"
});

// Limit sell order
await schwab.PlaceOrderAsync(new SchwabOrderRequest
{
    OrderType = "limit",
    Side = "sell",
    Qty = 5,
    Symbol = "AAPL",
    LimitPrice = 195.00m
});
```

#### Get Price History / 获取历史行情

```csharp
var history = await schwab.GetPriceHistoryAsync(
    symbol: "TSLA",
    startDate: DateTime.UtcNow.AddDays(-60),
    endDate: DateTime.UtcNow,
    frequencyType: "daily",          // minute | daily | weekly | monthly
    frequency: 1,                   // for "minute": 1,5,10,15,30
    needExtendedHoursData: false);   // exclude pre-market/after-hours
```

#### Market Status and Option Chain / 市场状态和期权链

```csharp
// Check if market is currently open
bool isOpen = await schwab.IsMarketOpenAsync();

// Get option chain for a symbol
var options = await schwab.GetOptionChainAsync("AAPL");
```

### 4.4 Alpaca — US Equity Broker / 美股经纪服务

**Service**: `IUSEquityBrokerService`

#### Account Information / 账户信息

```csharp
var alpaca = provider.GetService<IUSEquityBrokerService>();

// Get current account equity (total portfolio value)
decimal equity = await alpaca.GetAccountEquityAsync();

// Get formatted account summary (includes cash, buy power, etc.)
string summary = await alpaca.GetFormattedAccountSummaryAsync();

// Check if market is open
bool isOpen = await alpaca.IsMarketOpeningAsync();
```

#### Place Orders / 下单

```csharp
// Buy 100 shares of AAPL (market order)
await alpaca.PlaceOrderAsync(
    underlying: new Underlying { Symbol = "AAPL" },
    quality: 100,                          // positive = buy
    orderType: OrderExecutionType.Market,
    timeInForce: TimeInForce.GoodTillCanceled,
    afterHours: true                       // allow extended hours trading
);

// Sell 50 shares (negative quantity = sell)
await alpaca.PlaceOrderAsync(
    new Underlying { Symbol = "TSLA" },
    quality: -50,                          // negative = sell
    orderType: OrderExecutionType.Limit,   // limit order
    afterHours: false
);
```

#### Set Target Holdings / 设置目标仓位

Automatically buys or sells to reach a target portfolio percentage:

```csharp
// Adjust AAPL to be 10% of total portfolio
await alpaca.SetHoldingsAsync("AAPL", 0.10);
```

#### Liquidate / 清仓

```csharp
// Close all positions for a specific symbol
await alpaca.LiquidateAsync("TSLA");
```

### 4.5 Interactive Brokers / 盈透证券

**Service**: `IIBKRService` (via InterReact library)

> **Prerequisite**: You need an Interactive Brokers TWS/Gateway running and InterReact client configured with the correct server IP and port.

#### Get Account Summary / 获取账户摘要

```csharp
var ibkr = provider.GetService<IIBKRService>();

var summary = await ibkr.GetAccountSummaryAsync();
Console.WriteLine($"Total: {summary.TotalEquityValue:C}");
```

#### Place an Order / 下单

```csharp
// Market buy order for AAPL
int orderId = await ibkr.PlaceOrderAsync(
    order: new OrderBase
    {
        Symbol = "AAPL",
        ActionType = OrderActionType.Buy,
        Quantity = 100,
    },
    exchange: "SMART",                     // SMART routing for best execution
    securityType: ContractSecurityType.Stock,
    currency: Currency.USD);

Console.WriteLine($"Order placed with ID: {orderId}");
```

#### Get Positions / 获取持仓

```csharp
var positions = await ibkr.GetPositionAsync();
```

---

## 5. Analysis Module

**Service**: `IAnalysisService`

Provides statistical tests and quantitative analysis tools out of the box — no external math libraries needed for common use cases.

### 5.1 Correlation / 相关系数

```csharp
var analysis = provider.GetService<IAnalysisService>();

// Calculate Pearson correlation between two price series
double correlation = analysis.CalculateCorrelation(aaplPrices, msftPrices);

if (Math.Abs(correlation) > 0.8)
{
    Console.WriteLine("Highly correlated — potential pair trading candidates");
}
```

### 5.2 ADF Stationarity Test / ADF平稳性检验

Used in pair trading to test if the spread between two assets is mean-reverting:

```csharp
// Option 1: Pure C# implementation (recommended for speed)
bool isStationary = analysis.AugmentedDickeyFullerTest(spreadSeries, threshold: -2.86);

if (!isStationary)
{
    Console.WriteLine("Spread is NOT stationary — not suitable for mean reversion strategy");
}

// Option 2: Python-backed ADF (uses statsmodels via pythonnet, more accurate)
var adfResult = analysis.AugmentedDickeyFullerTestPython(spreadSeries);
Console.WriteLine($"ADF Statistic: {adfResult.Statistic:F4}, p-value: {adfResult.PValue:F6}");
```

### 5.3 OLS Regression / 线性回归

Fits a linear relationship between two series (useful for cointegration):

```csharp
var (slope, intercept) = analysis.PerformOLSRegression(aaplPrices, msftPrices);

Console.WriteLine($"Slope: {slope:F4}");
Console.WriteLine($"Intercept: {intercept:F4}");

// Calculate spread: diff = B - Slope * A - Intercept
var spread = msftPrices.Zip(aaplPrices, (b, a) => b - slope * a - intercept);
```

### 5.4 Z-Score / Z分数标准化

Normalize price data for threshold-based entry/exit signals:

```csharp
// Method 1: Calculate z-score of the entire series
var zScores = analysis.CalculateZScores(priceSeries, value);

// Method 2: Manual mean/std input
double zScore = analysis.CalculateZScores(mean: 150.0, stdDev: 5.0, value: 160.0);
// Result: zScore ≈ 2.0 (two standard deviations above mean)

if (Math.Abs(zScore) > 2.0)
{
    Console.WriteLine("Price is 2σ from mean — potential reversal signal");
}
```

### 5.5 Shapiro-Wilk Normality Test / 正态性检验

Tests if a dataset follows a normal distribution:

```csharp
bool isNormal = analysis.PerformShapiroWilkTest(returnSeries, threshold: 0.05);

if (isNormal)
{
    Console.WriteLine("Returns follow a normal distribution");
}
else
{
    Console.WriteLine("Returns have fat tails — consider non-parametric tests");
}
```

---

## 6. Notification Module

All notification services send messages asynchronously and return the HTTP response for verification:

### 6.1 DingTalk / 钉钉机器人

**Service**: `IDingtalkService`

```csharp
var dingTalk = provider.GetService<IDingtalkService>();

// Send a text notification to your DingTalk group
var response = await dingTalk.SendNotificationAsync(
    content: "[Strategy Alert] BTC/USDT broke above 50,000 — long signal triggered",
    accessToken: "your-dingtalk-webhook-access-token",
    secret: "your-signing-secret"
);

Console.WriteLine($"Sent: {response.IsSuccessful}");
```

### 6.2 WeChat Work / 企业微信

**Service**: `IWeChatService`

```csharp
var wechat = provider.GetService<IWeChatService>();

// Send a text notification to your WeChat Work group chat
var response = await wechat.SendTextNotificationAsync(
    content: "[Quant Alert] AAPL crossed 50-day moving average upward",
    webHook: "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=YOUR_WEBHOOK_KEY"
);

Console.WriteLine($"Sent: {response.IsSuccessful}");
```

### 6.3 Email / 邮件通知

**Service**: `IEmailService`

Supports both SMTP (Gmail, Outlook, etc.) and Brevo (formerly Sendinblue):

```csharp
var email = provider.GetService<IEmailService>();

// Send an HTML email via SMTP
var result = await email.SendBulkEmailAsync(
    message: new EmailMessage
    {
        Subject = "Daily Strategy Report — " + DateTime.Today.ToString("yyyy-MM-dd"),
        Body = @"
            <h1>Today's Trading Summary</h1>
            <ul>
                <li>AAPL: Long position opened at 175.50</li>
                <li>TSLA: Take-profit triggered, +3.2%</li>
            </ul>
        ",
        IsHtml = true,
        To = new List<string> { "trader@example.com" }
    },
    setting: new EmailSettingBase
    {
        SmtpServer = "smtp.gmail.com",
        Port = 587,
        SenderEmail = "you@gmail.com",
        Password = "your-app-password"  // Use app-specific password for Gmail
    }
);

Console.WriteLine($"Email sent: {result}");
```

> **Tip**: Store email passwords in .NET User Secrets, never in source code.

---

## 7. Portfolio Module

### 7.1 Binance Futures Portfolio / 币安期货账户信息

Access through `IBinanceUsdFutureService`:

```csharp
var futures = provider.GetService<IBinanceUsdFutureService>();

// Get total account balance in USD
decimal balance = await futures.GetusdFutureAccountBalanceAsync();

// Get unrealized profit/loss rate across all positions
double pnlRate = await futures.GetusdFutureUnrealizedProfitRateAsync();

// Get detailed position information
var positions = await futures.GetHoldingPositionAsync();
```

### 7.2 Alpaca Portfolio / Alpaca账户信息

Access through `IUSEquityBrokerService`:

```csharp
var alpaca = provider.GetService<IUSEquityBrokerService>();

// Get current equity (cash + market value of positions)
decimal equity = await alpaca.GetAccountEquityAsync();

// Get formatted summary including cash balance, buy power, day trade count
string summary = await alpaca.GetFormattedAccountSummaryAsync();
Console.WriteLine(summary);

// Check individual position
var pos = await alpaca.GetPositionAsync("AAPL");
```

---

## 8. Common Usage Patterns

### 8.1 Complete Strategy Skeleton / 策略模板

This pattern demonstrates a typical quantitative strategy flow: fetch data → analyze → execute → notify:

```csharp
class PairTradingStrategy
{
    private readonly ITraditionalFinanceSourceDataService _dataService;
    private readonly IAnalysisService _analysis;
    private readonly IBinanceOrderService _orderService;
    private readonly IDingtalkService _dingTalk;

    public PairTradingStrategy(IServiceProvider provider)
    {
        _dataService = provider.GetService<ITraditionalFinanceSourceDataService>();
        _analysis = provider.GetService<IAnalysisService>();
        _orderService = provider.GetService<IBinanceOrderService>();
        _dingTalk = provider.GetService<IDingtalkService>();
    }

    public async Task RunAsync()
    {
        // 1. Fetch data for two crypto pairs
        var btcBars = await _dataService.DownloadOhlcvListAsync("BTC", ...);
        var ethBars = await _dataService.DownloadOhlcvListAsync("ETH", ...);

        // 2. Test stationarity of the spread
        bool isStationary = _analysis.AugmentedDickeyFullerTest(spread);

        // 3. If stationary and z-score threshold breached, execute trades
        if (isStationary && Math.Abs(zScore) > 2.0)
        {
            // Open positions...
            await _orderService.PlaceUsdFutureOrderAsync(...);

            // Send notification
            await _dingTalk.SendNotificationAsync("[Pair Trade] BTC/ETH opened", token, secret);
        }
    }
}
```

### 8.2 Error Handling / 错误处理

All network operations use **Polly retry with exponential backoff** (3 retries: 2s, 4s, 8s delay) for transient failures like rate limits or temporary network issues:

```csharp
try
{
    var result = await dataService.DownloadOhlcvListAsync("AAPL", ...);
}
catch (HttpRequestException ex)
{
    // Network error — all retry attempts exhausted
    Console.WriteLine($"Failed after 3 retries: {ex.Message}");
}
catch (Exception ex)
{
    // Non-retryable error (e.g., invalid parameters)
    Console.WriteLine($"Error: {ex.Message}");
}
```

### 8.3 Running Tests / 运行测试

Exclude Binance tests if you're in a region where the API is blocked:

```bash
# Run all tests except Binance-related ones
dotnet test --filter "FullyQualifiedName!~Binance"

# Run only Analysis module tests
dotnet test --filter "FullyQualifiedName~Analysis"
```

---

## 9. Troubleshooting and FAQ

### Q1: Yahoo Finance data download fails with Python errors

**Cause**: The `pythonnet` bridge requires a Python installation with the `yfinance` package.

**Fix**:
```bash
# Install yfinance in your Python environment
pip install yfinance

# Verify the Python path is correct in appsettings.json
{
  "YahooFinance": {
    "PythonPath": "C:\\Users\\you\\Anaconda3\\python.exe"
  }
}
```

### Q2: Binance API returns 401 or 403 errors

**Cause**: Incorrect API key/secret, expired credentials, or IP restriction.

**Fix**:
- Verify your keys in the Binance dashboard
- Check that `BinanceApi:Environment` matches your account type (testnet vs live)
- For mainland China: Binance API may be inaccessible — use a VPN or Singapore IP
- Testnet and live credentials are separate — ensure you're using the correct set

### Q3: "Service not registered" when resolving a service

**Cause**: `services.AddQuantInfraNet()` was not called, or the service provider was built before registration.

**Fix**: Always call `AddQuantInfraNet()` before `BuildServiceProvider()`:

```csharp
var services = new ServiceCollection();
services.AddQuantInfraNet();           // ← must come first
var provider = services.BuildServiceProvider();
```

### Q4: Interactive Brokers connection fails

**Fix**:
1. Ensure TWS or IB Gateway is running and accepting API connections
2. Verify the server IP and port in `InterReactOptions` match your TWS settings
3. Check that "Enable Active X and Socket Clients" is checked in TWS → Global Configuration → API → Settings

### Q5: Schwab authentication token expired

**Fix**: The Schwab access token has a limited lifetime (typically 30 minutes). You need to re-authenticate by obtaining a new OAuth token. Set up a cron job or timer in your application to refresh tokens periodically.

---

## Appendix — Resolution Levels

The `ResolutionLevel` enum controls the granularity of OHLCV data:

| Level | Description / 说明 | Typical use case |
|-------|-------------------|-----------------|
| `Minute` | 1-minute bars | Intraday/HFT strategies |
| `Hourly` | 1-hour bars | Swing trading, short-term analysis |
| `Daily` | 1-day bars | Position trading, backtesting |

When requesting data from Yahoo Finance or Binance, specify the resolution level. The service will return the appropriate granularity:

```csharp
// Get 1-minute intraday data (only available for recent periods)
var minuteData = await dataService.DownloadOhlcvListAsync(
    "AAPL", DateTime.Today, DateTime.Now, ResolutionLevel.Minute);

// Get daily data (available for years of history)
var dailyData = await dataService.DownloadOhlcvListAsync(
    "AAPL", DateTime.UtcNow.AddYears(-5), DateTime.UtcNow, ResolutionLevel.Daily);
```

---

## License

[MIT](../LICENSE) — Copyright © 2024–2026 Rong (Rex) Fan

> **Disclaimer**: See [DISCLAIMER](./Disclaimer.md) for full disclaimer and limitation of liability. This library provides technical solutions only — you are responsible for your own trading decisions and compliance with local regulations.
