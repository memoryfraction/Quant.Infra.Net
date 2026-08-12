# Quant.Infra.Net User Manual / 用户使用手册

---

## Contents / 目录

1. [Getting Started](#getting-started) / [快速开始](#getting-started-cn)
2. [Data Source Module](#data-source-module) / [数据源模块](#data-source-module-cn)
3. [Broker Module](#broker-module) / [券商模块](#broker-module-cn)
4. [Analysis Module](#analysis-module) / [分析模块](#analysis-module-cn)
5. [Portfolio Module](#portfolio-module) / [组合模块](#portfolio-module-cn)
6. [Notification Module](#notification-module) / [通知模块](#notification-module-cn)
7. [Configuration Guide](#configuration-guide) / [配置指南](#configuration-guide-cn)

---

## Getting Started <a name="getting-started"></a>

### Prerequisites

- **.NET 8.0 SDK** or later / .NET 8.0 SDK 或更高版本
- **Python 3.x** (optional, for Yahoo Finance data source) / Python 3.x（可选，用于 Yahoo Finance 数据源）

### Installation

```bash
# Create a new console project
dotnet new console -n MyQuantApp
cd MyQuantApp

# Install Quant.Infra.Net from NuGet
dotnet add package Quant.Infra.Net --version 1.5.1

# Required for Python-based data sources (Yahoo Finance via yfinance)
dotnet add package pythonnet

# Recommended for dependency injection
dotnet add package Microsoft.Extensions.DependencyInjection
```

### Basic Usage Pattern

All services are registered via Dependency Injection:

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddQuantInfraNet(); // Registers all modules
var provider = services.BuildServiceProvider();
```

> **Note**: All network operations use Polly retry with exponential backoff (3 retries, 2s/4s/8s delay) to handle transient failures.

---

## Data Source Module <a name="data-source-module"></a>

### Yahoo Finance Data

```csharp
using Quant.Infra.Net.SourceData.Service;

var dataService = provider.GetService<ITraditionalFinanceSourceDataService>();
var bars = await dataService.GetOhlcvListAsync("AAPL", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
// Expected: List of OHLCV bars for AAPL over the past 30 days
```

### Binance Crypto Data

```csharp
var cryptoService = provider.GetService<ICryptoSourceDataService>();

// Download spot data for multiple symbols
await cryptoService.DownloadBinanceSpotAsync(
    new[] { "BTCUSDT", "ETHUSDT" },
    DateTime.UtcNow.AddDays(-7),
    DateTime.UtcNow,
    path: "./data/",           // CSV output directory
    klineInterval: KlineInterval.OneHour
);

// Download USD futures data
await cryptoService.DownloadBinanceUsdFutureAsync(
    new[] { "BTCUSDT", "ETHUSDT" },
    DateTime.UtcNow.AddDays(-7),
    DateTime.UtcNow,
    path: "./futures_data/"
);
```

> **Expected Output**: CSV files containing OHLCV data for each symbol-interval combination.

---

## Broker Module <a name="broker-module"></a>

### Binance Futures (Testnet)

```csharp
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Shared.Model;

// Set environment to testnet first!
var config = provider.GetService<IConfiguration>();
config["BinanceApi:Environment"] = "testnet";

var binanceService = provider.GetService<IBinanceFuturesService>();
await binanceService.PlaceOrderAsync(
    symbol: "BTCUSDT",
    side: Side.Buy,
    quantity: 0.001m,
    price: null  // Market order
);
```

### Charles Schwab

```csharp
var schwabService = provider.GetService<ISchwabBrokerService>();

// Get real-time quote
var quote = await schwabService.GetQuoteAsync("AAPL");

// Place a market order
var order = new OrderBase
{
    Symbol = "MSFT",
    ActionType = OrderActionType.Buy,
    Quantity = 10,
    ExecutionType = OrderExecutionType.Market
};
int orderId = await schwabService.PlaceOrderAsync(order);
```

### Interactive Brokers (via InterReact)

```csharp
var ibkrService = provider.GetService<IIBKRService>();

// Get account summary
var summary = await ibkrService.GetAccountSummaryAsync();

// Place a limit order
int orderId = await ibkrService.PlaceOrderAsync(
    order: new OrderBase
    {
        Symbol = "AAPL",
        ActionType = OrderActionType.Buy,
        Quantity = 100,
        Price = 150.0m,
        ExecutionType = OrderExecutionType.Limit
    }
);
```

---

## Analysis Module <a name="analysis-module"></a>

### Statistical Tests

```csharp
using Quant.Infra.Net.Analysis.Service;

var analysisService = provider.GetService<IAnalysisService>();

// ADF Stationarity Test (for pair trading)
var isStationary = await analysisService.TestStationarityAsync(spreadSeries);
if (!isStationary)
    Console.WriteLine("Spread is not stationary - not suitable for mean reversion");

// OLS Regression
var regression = await analysisService.CalculateRegressionAsync(independent, dependent);
Console.WriteLine($"R-squared: {regression.RSquared}");

// Z-Score Normalization
var zScores = await analysisService.CalculateZScoreAsync(prices, window: 20);
```

---

## Portfolio Module <a name="portfolio-module"></a>

### Performance Analytics

```csharp
using Quant.Infra.Net.Portfolio;

var portfolioService = provider.GetService<IPortfolioSnapshotService>();
var snapshot = await portfolioService.GetSnapshotAsync(accountId);

Console.WriteLine($"Portfolio Value: {snapshot.TotalValue:C}");
Console.WriteLine($"CAGR: {snapshot.CAGR:P2}");
Console.WriteLine($"Sharpe Ratio: {snapshot.SharpeRatio:F2}");
Console.WriteLine($"Max Drawdown: {snapshot.MaxDrawdown:P2}");
```

---

## Notification Module <a name="notification-module"></a>

### DingTalk Notifications

```csharp
using Quant.Infra.Net.Notification.Service;

var dingTalkService = provider.GetService<IDingtalkService>();
var response = await dingTalkService.SendNotificationAsync(
    content: "[Strategy Alert] AAPL crossed above 200-day moving average",
    accessToken: "your-dingtalk-access-token",
    secret: "your-signing-secret"
);

Console.WriteLine($"Sent: {{response.IsSuccessful}}");
```

### WeChat Work Notifications

```csharp
var wechatService = provider.GetService<IWeChatService>();
var response = await wechatService.SendTextNotificationAsync(
    content: "[Quant] BTC/USDT price alert triggered",
    webHook: "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=YOUR_KEY"
);
```

### Email Notifications (Personal)

```csharp
using Quant.Infra.Net.Notification.Model;

var emailService = provider.GetService<IEmailService>();
var result = await emailService.SendBulkEmailAsync(
    message: new EmailMessage
    {
        Subject = "Daily Strategy Report",
        Body = "<h1>Today's Results</h1><p>...report content...</p>",
        IsHtml = true,
        To = new List<string> { "trader@example.com" }
    },
    setting: new EmailSettingBase
    {
        SmtpServer = "smtp.gmail.com",
        Port = 587,
        SenderEmail = "you@gmail.com",
        Password = "your-app-password"
    }
);
```

---

## Configuration Guide <a name="configuration-guide"></a>

### User Secrets (Development)

```bash
# Binance credentials
dotnet user-secrets set "BinanceApi:ApiKey" "your-api-key"
dotnet user-secrets set "BinanceApi:SecretKey" "your-secret-key"
dotnet user-secrets set "BinanceApi:Environment" "testnet"

# DingTalk credentials
dotnet user-secrets set "DingTalk:AccessToken" "your-token"
dotnet user-secrets set "DingTalk:Secret" "your-secret"
```

### App Settings (Production)

Create `appsettings.json`:

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
  "Email": {
    "Personal": {
      "SmtpServer": "smtp.gmail.com",
      "Port": 587,
      "SenderEmail": "you@gmail.com"
    }
  }
}
```

---

## Notes on Testing <a name="testing-notes"></a>

> ⚠️ **Binance Unit Tests**: The Binance integration tests require a **Singapore IP address** to pass. They will fail when run from China or the United States due to regional access restrictions on Binance API endpoints.

```bash
# Run tests excluding Binance tests:
dotnet test --filter "FullyQualifiedName!~Binance"
```

---

## License

[MIT](LICENSE) - Copyright (c) 2024-2026 Rong (Rex) Fan

> **Disclaimer**: See [DISCLAIMER.md](docs/DISCLAIMER.md) for full disclaimer and limitation of liability.
