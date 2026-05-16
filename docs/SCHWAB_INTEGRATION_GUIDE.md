# Charles Schwab 券商集成指南

## 📋 功能概览

本集成提供了与 Charles Schwab 券商的完整交互功能，包括：

✅ **账户管理**
- 获取账户信息（余额、市值、购买力等）
- 查看账户类型和状态

✅ **持仓管理**
- 获取所有持仓信息
- 查询特定标的持仓
- 实时计算盈亏

✅ **行情数据**
- 获取实时股票报价
- 批量获取多个标的报价
- 支持盘前盘后数据

✅ **期权链**
- 获取完整期权链数据
- 支持 Call/Put 筛选
- 包含希腊字母（Delta, Gamma, Theta, Vega, Rho）
- 隐含波动率数据
- 未平仓合约和成交量

✅ **交易功能**
- 下单（市价单、限价单、止损单）
- 查询订单状态
- 取消订单
- 获取历史订单

✅ **市场状态**
- 检查市场是否开盘
- 支持盘前盘后交易

## 🏗️ 架构设计

### 接口定义
```
ISchwabBrokerService
├── GetAccountAsync()              // 获取账户信息
├── GetPositionsAsync()            // 获取所有持仓
├── GetPositionAsync(symbol)       // 获取单个持仓
├── GetQuoteAsync(symbol)          // 获取报价
├── GetQuotesAsync(symbols)        // 批量获取报价
├── GetOptionChainAsync(...)       // 获取期权链
├── PlaceOrderAsync(order)         // 下单
├── GetOrderAsync(orderId)         // 查询订单
├── CancelOrderAsync(orderId)      // 取消订单
├── GetOrdersAsync(maxResults)     // 获取订单列表
└── IsMarketOpenAsync()            // 检查市场状态
```

### 数据模型

#### SchwabAccount（账户信息）
```csharp
public class SchwabAccount
{
    public string AccountNumber { get; set; }      // 账户号码
    public string AccountType { get; set; }        // 账户类型
    public decimal CashBalance { get; set; }       // 现金余额
    public decimal MarketValue { get; set; }       // 市值
    public decimal TotalEquity { get; set; }       // 总资产
    public decimal BuyingPower { get; set; }       // 购买力
    public decimal UnrealizedPnL { get; set; }     // 未实现盈亏
    public decimal RealizedPnL { get; set; }       // 已实现盈亏
}
```

#### SchwabQuote（股票报价）
```csharp
public class SchwabQuote
{
    public string Symbol { get; set; }             // 标的代码
    public decimal BidPrice { get; set; }          // 买价
    public decimal AskPrice { get; set; }          // 卖价
    public decimal LastPrice { get; set; }         // 最新价
    public long Volume { get; set; }               // 成交量
    public decimal High { get; set; }              // 最高价
    public decimal Low { get; set; }               // 最低价
    public decimal Open { get; set; }              // 开盘价
    public decimal Close { get; set; }             // 收盘价
    public decimal Change { get; set; }            // 涨跌额
    public decimal ChangePercent { get; set; }     // 涨跌幅
    public long Timestamp { get; set; }            // 时间戳
}
```

#### SchwabOptionChain（期权链）
```csharp
public class SchwabOptionChain
{
    public string Symbol { get; set; }                          // 标的代码
    public string Status { get; set; }                          // 状态
    public decimal UnderlyingPrice { get; set; }                // 标的价格
    public List<SchwabOptionContract> CallOptions { get; set; } // Call 期权
    public List<SchwabOptionContract> PutOptions { get; set; }  // Put 期权
}
```

#### SchwabOptionContract（期权合约）
```csharp
public class SchwabOptionContract
{
    public string Symbol { get; set; }             // 期权代码
    public string Description { get; set; }        // 描述
    public string ExpirationDate { get; set; }     // 到期日
    public decimal Strike { get; set; }            // 行权价
    public string ContractType { get; set; }       // CALL/PUT
    public decimal Bid { get; set; }               // 买价
    public decimal Ask { get; set; }               // 卖价
    public decimal Last { get; set; }              // 最新价
    public decimal Mark { get; set; }              // 标记价
    public long Volume { get; set; }               // 成交量
    public long OpenInterest { get; set; }         // 未平仓合约
    public decimal ImpliedVolatility { get; set; } // 隐含波动率
    public decimal Delta { get; set; }             // Delta
    public decimal Gamma { get; set; }             // Gamma
    public decimal Theta { get; set; }             // Theta
    public decimal Vega { get; set; }              // Vega
    public decimal Rho { get; set; }               // Rho
    public bool InTheMoney { get; set; }           // 是否价内
}
```

## 🔧 配置说明

### 1. 获取 Schwab API 凭据

1. 访问 [Charles Schwab Developer Portal](https://developer.schwab.com/)
2. 注册开发者账户
3. 创建应用程序
4. 获取 API Key 和 Secret
5. 记录账户号码

### 2. 配置用户机密

在项目根目录执行：

```bash
# 初始化用户机密
dotnet user-secrets init --project src/Quant.Infra.Net.Tests

# 设置 Schwab API Key
dotnet user-secrets set "Schwab:ApiKey" "your-api-key" --project src/Quant.Infra.Net.Tests

# 设置 Schwab Secret
dotnet user-secrets set "Schwab:Secret" "your-api-secret" --project src/Quant.Infra.Net.Tests

# 设置账户号码
dotnet user-secrets set "Schwab:AccountNumber" "your-account-number" --project src/Quant.Infra.Net.Tests

# 设置 API 基础 URL（可选，默认为生产环境）
dotnet user-secrets set "Schwab:BaseUrl" "https://api.schwabapi.com/trader/v1" --project src/Quant.Infra.Net.Tests
```

### 3. appsettings.test.json 配置

```json
{
  "Schwab": {
    "BaseUrl": "https://api.schwabapi.com/trader/v1"
  }
}
```

**注意**：敏感信息（ApiKey, Secret, AccountNumber）应该存储在用户机密中，不要提交到代码仓库。

## 📝 使用示例

### 示例 1：获取账户信息

```csharp
var credentials = new BrokerCredentials
{
    ApiKey = "your-api-key",
    Secret = "your-api-secret",
    BaseUrl = "https://api.schwabapi.com/trader/v1"
};

var schwabService = new SchwabBrokerService(credentials, "your-account-number");

// 获取账户信息
var account = await schwabService.GetAccountAsync();
Console.WriteLine($"总资产: ${account.TotalEquity:N2}");
Console.WriteLine($"现金余额: ${account.CashBalance:N2}");
Console.WriteLine($"市值: ${account.MarketValue:N2}");
Console.WriteLine($"购买力: ${account.BuyingPower:N2}");
```

### 示例 2：获取持仓信息

```csharp
// 获取所有持仓
var positions = await schwabService.GetPositionsAsync();

foreach (var position in positions)
{
    Console.WriteLine($"{position.Symbol}: {position.Quantity} 股");
    Console.WriteLine($"  成本价: ${position.CostPrice:N2}");
    Console.WriteLine($"  资产类型: {position.AssetType}");
}

// 获取特定标的持仓
var aaplPosition = await schwabService.GetPositionAsync("AAPL");
if (aaplPosition != null)
{
    Console.WriteLine($"AAPL 持仓: {aaplPosition.Quantity} 股");
}
```

### 示例 3：获取股票报价

```csharp
// 单个报价
var quote = await schwabService.GetQuoteAsync("AAPL");
Console.WriteLine($"AAPL 最新价: ${quote.LastPrice:N2}");
Console.WriteLine($"涨跌幅: {quote.ChangePercent:N2}%");

// 批量报价
var symbols = new List<string> { "AAPL", "MSFT", "GOOGL", "TSLA" };
var quotes = await schwabService.GetQuotesAsync(symbols);

foreach (var kvp in quotes)
{
    Console.WriteLine($"{kvp.Key}: ${kvp.Value.LastPrice:N2}");
}
```

### 示例 4：获取期权链

```csharp
// 获取完整期权链
var optionChain = await schwabService.GetOptionChainAsync("AAPL");
Console.WriteLine($"标的价格: ${optionChain.UnderlyingPrice:N2}");
Console.WriteLine($"Call 期权数量: {optionChain.CallOptions.Count}");
Console.WriteLine($"Put 期权数量: {optionChain.PutOptions.Count}");

// 只获取 Call 期权，限制行权价数量
var callChain = await schwabService.GetOptionChainAsync(
    "AAPL", 
    contractType: "CALL", 
    strikeCount: 5
);

// 查找价内期权
var itmCalls = callChain.CallOptions.Where(c => c.InTheMoney).ToList();
Console.WriteLine($"价内 Call 期权: {itmCalls.Count} 个");

// 按 Delta 排序
var sortedByDelta = callChain.CallOptions
    .OrderByDescending(c => Math.Abs(c.Delta))
    .Take(10)
    .ToList();
```

### 示例 5：下单交易

```csharp
// 市价买入
var buyOrder = new SchwabOrderRequest
{
    Symbol = "AAPL",
    OrderType = "MARKET",
    Side = "BUY",
    Quantity = 10,
    TimeInForce = "DAY",
    AssetType = "EQUITY"
};

var orderId = await schwabService.PlaceOrderAsync(buyOrder);
Console.WriteLine($"订单已提交: {orderId}");

// 限价卖出
var sellOrder = new SchwabOrderRequest
{
    Symbol = "AAPL",
    OrderType = "LIMIT",
    Side = "SELL",
    Quantity = 10,
    LimitPrice = 180.00m,
    TimeInForce = "GTC",
    AssetType = "EQUITY"
};

var sellOrderId = await schwabService.PlaceOrderAsync(sellOrder);

// 查询订单状态
var order = await schwabService.GetOrderAsync(sellOrderId);
Console.WriteLine($"订单状态: {order.Status}");
Console.WriteLine($"已成交: {order.FilledQuantity}/{order.Quantity}");
```

### 示例 6：持仓分析（含实时报价）

```csharp
// 获取持仓
var positions = await schwabService.GetPositionsAsync();

// 获取所有持仓标的的报价
var symbols = positions.Select(p => p.Symbol).ToList();
var quotes = await schwabService.GetQuotesAsync(symbols);

// 计算盈亏
decimal totalCost = 0;
decimal totalMarketValue = 0;

foreach (var position in positions)
{
    if (quotes.TryGetValue(position.Symbol, out var quote))
    {
        var marketValue = position.Quantity * quote.LastPrice;
        var costValue = position.Quantity * position.CostPrice;
        var pnl = marketValue - costValue;
        var pnlPercent = (pnl / costValue) * 100;

        totalCost += costValue;
        totalMarketValue += marketValue;

        Console.WriteLine($"{position.Symbol}:");
        Console.WriteLine($"  持仓: {position.Quantity} 股");
        Console.WriteLine($"  成本: ${costValue:N2}");
        Console.WriteLine($"  市值: ${marketValue:N2}");
        Console.WriteLine($"  盈亏: ${pnl:N2} ({pnlPercent:+0.00;-0.00}%)");
    }
}

var totalPnl = totalMarketValue - totalCost;
var totalPnlPercent = (totalPnl / totalCost) * 100;
Console.WriteLine($"\n总计:");
Console.WriteLine($"  总成本: ${totalCost:N2}");
Console.WriteLine($"  总市值: ${totalMarketValue:N2}");
Console.WriteLine($"  总盈亏: ${totalPnl:N2} ({totalPnlPercent:+0.00;-0.00}%)");
```

### 示例 7：期权策略分析

```csharp
// 获取期权链
var optionChain = await schwabService.GetOptionChainAsync("SPY", strikeCount: 10);

// 找出最活跃的期权（按成交量）
var mostActiveOptions = optionChain.CallOptions
    .OrderByDescending(c => c.Volume)
    .Take(5)
    .ToList();

Console.WriteLine("最活跃的 Call 期权:");
foreach (var option in mostActiveOptions)
{
    Console.WriteLine($"{option.Symbol}");
    Console.WriteLine($"  行权价: ${option.Strike:N2}");
    Console.WriteLine($"  成交量: {option.Volume:N0}");
    Console.WriteLine($"  未平仓: {option.OpenInterest:N0}");
    Console.WriteLine($"  隐含波动率: {option.ImpliedVolatility:P2}");
}

// 找出高 Delta 的期权（接近实值）
var highDeltaOptions = optionChain.CallOptions
    .Where(c => c.Delta >= 0.7m && c.Delta <= 0.9m)
    .OrderByDescending(c => c.Delta)
    .ToList();

// 计算跨式策略（Straddle）
var atmStrike = optionChain.CallOptions
    .OrderBy(c => Math.Abs(c.Strike - optionChain.UnderlyingPrice))
    .First()
    .Strike;

var atmCall = optionChain.CallOptions.First(c => c.Strike == atmStrike);
var atmPut = optionChain.PutOptions.First(p => p.Strike == atmStrike);

var straddleCost = atmCall.Ask + atmPut.Ask;
Console.WriteLine($"\nATM Straddle (行权价 ${atmStrike:N2}):");
Console.WriteLine($"  Call 价格: ${atmCall.Ask:N2}");
Console.WriteLine($"  Put 价格: ${atmPut.Ask:N2}");
Console.WriteLine($"  总成本: ${straddleCost:N2}");
Console.WriteLine($"  盈亏平衡点: ${atmStrike - straddleCost:N2} / ${atmStrike + straddleCost:N2}");
```

## 🧪 运行测试

```bash
# 运行所有 Schwab 测试
cd src/Quant.Infra.Net.Tests
dotnet test --filter "FullyQualifiedName~SchwabBrokerServiceTests"

# 运行特定测试
dotnet test --filter "Test_GetAccount"
dotnet test --filter "Test_GetPositions"
dotnet test --filter "Test_GetOptionChain"
```

## 🔒 安全注意事项

1. **凭据管理**
   - ✅ 使用用户机密存储敏感信息
   - ✅ 不要将 API Key 提交到代码仓库
   - ✅ 生产环境使用环境变量或密钥管理服务

2. **访问令牌**
   - ✅ 自动刷新过期令牌
   - ✅ 令牌缓存机制
   - ✅ 提前 60 秒刷新令牌

3. **交易安全**
   - ⚠️ 下单测试默认被忽略（`[Ignore]` 特性）
   - ⚠️ 生产环境下单前务必确认参数
   - ⚠️ 建议先在模拟账户测试

## 📊 API 限制

Charles Schwab API 有以下限制：

- **请求频率**：每秒最多 120 次请求
- **令牌有效期**：通常为 30 分钟
- **市场数据延迟**：实时数据（需要订阅）
- **历史数据**：最多获取 1 年历史数据

## 🐛 故障排除

### 问题 1：认证失败
```
错误: 401 Unauthorized
```
**解决方案**：
- 检查 API Key 和 Secret 是否正确
- 确认账户号码是否正确
- 检查 API 权限设置

### 问题 2：找不到持仓
```
返回空列表
```
**解决方案**：
- 确认账户中确实有持仓
- 检查账户号码是否正确
- 确认使用的是正确的环境（生产/测试）

### 问题 3：期权链数据为空
```
CallOptions 和 PutOptions 都为空
```
**解决方案**：
- 确认标的有期权交易
- 检查是否在交易时间
- 尝试不同的 strikeCount 参数

## 📚 相关资源

- [Schwab Developer Portal](https://developer.schwab.com/)
- [Schwab API 文档](https://developer.schwab.com/products/trader-api--individual)
- [OAuth 2.0 认证指南](https://developer.schwab.com/products/trader-api--individual/details/documentation/Retail%20Trader%20API%20Production)

## 🎯 下一步计划

- [ ] 添加实时行情 WebSocket 支持
- [ ] 支持更多订单类型（括号单、OCO 等）
- [ ] 添加期权策略构建器
- [ ] 集成风险管理功能
- [ ] 添加回测数据接口
- [ ] 支持多账户管理

---

**版本**: 1.0.0  
**最后更新**: 2026-04-19  
**维护者**: Quant.Infra.Net Team
