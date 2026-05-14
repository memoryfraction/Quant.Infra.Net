# Charles Schwab 集成 - 快速开始

## 🚀 5 分钟快速上手

### 步骤 1：获取 API 凭据（5 分钟）

1. 访问 [Schwab Developer Portal](https://developer.schwab.com/)
2. 注册并登录开发者账户
3. 创建新应用程序
4. 记录以下信息：
   - API Key
   - API Secret
   - 账户号码

### 步骤 2：配置项目（2 分钟）

```bash
# 进入测试项目目录
cd src/Quant.Infra.Net.Tests

# 初始化用户机密
dotnet user-secrets init

# 设置 Schwab 凭据
dotnet user-secrets set "Schwab:ApiKey" "your-api-key-here"
dotnet user-secrets set "Schwab:Secret" "your-api-secret-here"
dotnet user-secrets set "Schwab:AccountNumber" "your-account-number"
```

### 步骤 3：运行测试（1 分钟）

```bash
# 测试账户信息
dotnet test --filter "Test_GetAccount"

# 测试持仓信息
dotnet test --filter "Test_GetPositions"

# 测试期权链
dotnet test --filter "Test_GetOptionChain"
```

## 💡 常用代码片段

### 1. 初始化服务

```csharp
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Broker.Service;

var credentials = new BrokerCredentials
{
    ApiKey = "your-api-key",
    Secret = "your-api-secret",
    BaseUrl = "https://api.schwabapi.com/trader/v1"
};

ISchwabBrokerService schwab = new SchwabBrokerService(
    credentials, 
    "your-account-number"
);
```

### 2. 查看账户和持仓

```csharp
// 获取账户信息
var account = await schwab.GetAccountAsync();
Console.WriteLine($"总资产: ${account.TotalEquity:N2}");
Console.WriteLine($"现金: ${account.CashBalance:N2}");
Console.WriteLine($"市值: ${account.MarketValue:N2}");

// 获取所有持仓
var positions = await schwab.GetPositionsAsync();
foreach (var pos in positions)
{
    Console.WriteLine($"{pos.Symbol}: {pos.Quantity} 股 @ ${pos.CostPrice:N2}");
}
```

### 3. 获取股票报价

```csharp
// 单个报价
var quote = await schwab.GetQuoteAsync("AAPL");
Console.WriteLine($"AAPL: ${quote.LastPrice:N2} ({quote.ChangePercent:+0.00;-0.00}%)");

// 批量报价
var quotes = await schwab.GetQuotesAsync(new List<string> { "AAPL", "MSFT", "GOOGL" });
foreach (var q in quotes)
{
    Console.WriteLine($"{q.Key}: ${q.Value.LastPrice:N2}");
}
```

### 4. 获取期权链

```csharp
// 获取 AAPL 的期权链
var chain = await schwab.GetOptionChainAsync("AAPL", strikeCount: 5);

Console.WriteLine($"标的价格: ${chain.UnderlyingPrice:N2}");
Console.WriteLine($"Call 期权: {chain.CallOptions.Count} 个");
Console.WriteLine($"Put 期权: {chain.PutOptions.Count} 个");

// 显示价内 Call 期权
var itmCalls = chain.CallOptions.Where(c => c.InTheMoney).ToList();
foreach (var call in itmCalls)
{
    Console.WriteLine($"{call.Symbol}: 行权价 ${call.Strike:N2}, Delta {call.Delta:N4}");
}
```

### 5. 持仓盈亏分析

```csharp
// 获取持仓和报价
var positions = await schwab.GetPositionsAsync();
var symbols = positions.Select(p => p.Symbol).ToList();
var quotes = await schwab.GetQuotesAsync(symbols);

// 计算盈亏
decimal totalPnL = 0;
foreach (var pos in positions)
{
    if (quotes.TryGetValue(pos.Symbol, out var quote))
    {
        var pnl = (quote.LastPrice - pos.CostPrice) * pos.Quantity;
        var pnlPct = (pnl / (pos.CostPrice * pos.Quantity)) * 100;
        totalPnL += pnl;
        
        Console.WriteLine($"{pos.Symbol}: ${pnl:N2} ({pnlPct:+0.00;-0.00}%)");
    }
}
Console.WriteLine($"总盈亏: ${totalPnL:N2}");
```

## 🎯 实用场景

### 场景 1：监控持仓盈亏

```csharp
public async Task MonitorPositions()
{
    while (true)
    {
        var positions = await schwab.GetPositionsAsync();
        var symbols = positions.Select(p => p.Symbol).ToList();
        var quotes = await schwab.GetQuotesAsync(symbols);
        
        foreach (var pos in positions)
        {
            if (quotes.TryGetValue(pos.Symbol, out var quote))
            {
                var pnl = (quote.LastPrice - pos.CostPrice) * pos.Quantity;
                Console.WriteLine($"{pos.Symbol}: ${pnl:N2}");
            }
        }
        
        await Task.Delay(TimeSpan.FromMinutes(1)); // 每分钟更新
    }
}
```

### 场景 2：寻找高 Delta 期权

```csharp
public async Task<List<SchwabOptionContract>> FindHighDeltaOptions(string symbol)
{
    var chain = await schwab.GetOptionChainAsync(symbol);
    
    return chain.CallOptions
        .Where(c => c.Delta >= 0.7m && c.Delta <= 0.9m)
        .OrderByDescending(c => c.Volume)
        .Take(10)
        .ToList();
}
```

### 场景 3：期权策略分析

```csharp
public async Task AnalyzeStraddle(string symbol)
{
    var chain = await schwab.GetOptionChainAsync(symbol);
    
    // 找到最接近 ATM 的行权价
    var atmStrike = chain.CallOptions
        .OrderBy(c => Math.Abs(c.Strike - chain.UnderlyingPrice))
        .First()
        .Strike;
    
    var atmCall = chain.CallOptions.First(c => c.Strike == atmStrike);
    var atmPut = chain.PutOptions.First(p => p.Strike == atmStrike);
    
    var straddleCost = atmCall.Ask + atmPut.Ask;
    var breakEvenLow = atmStrike - straddleCost;
    var breakEvenHigh = atmStrike + straddleCost;
    
    Console.WriteLine($"Straddle 策略 (行权价 ${atmStrike:N2}):");
    Console.WriteLine($"  总成本: ${straddleCost:N2}");
    Console.WriteLine($"  盈亏平衡: ${breakEvenLow:N2} - ${breakEvenHigh:N2}");
}
```

## 📊 测试清单

运行以下测试确保一切正常：

```bash
# ✅ 账户信息
dotnet test --filter "Test_GetAccount"

# ✅ 持仓列表
dotnet test --filter "Test_GetPositions"

# ✅ 股票报价
dotnet test --filter "Test_GetQuote"

# ✅ 批量报价
dotnet test --filter "Test_GetQuotes_Multiple"

# ✅ 期权链
dotnet test --filter "Test_GetOptionChain"

# ✅ 市场状态
dotnet test --filter "Test_IsMarketOpen"

# ✅ 持仓+报价综合
dotnet test --filter "Test_GetPositions_WithQuotes"
```

## ⚠️ 注意事项

### 1. API 限制
- 每秒最多 120 次请求
- 令牌有效期 30 分钟
- 自动刷新机制已实现

### 2. 交易安全
- 下单测试默认被忽略（`[Ignore]` 特性）
- 生产环境下单前务必确认参数
- 建议先在模拟账户测试

### 3. 数据延迟
- 实时数据需要订阅
- 免费账户可能有延迟
- 期权数据更新频率较低

## 🐛 常见问题

### Q1: 认证失败怎么办？
**A**: 检查 API Key 和 Secret 是否正确，确认账户号码无误。

### Q2: 找不到持仓？
**A**: 确认账户中有持仓，检查是否使用正确的环境（生产/测试）。

### Q3: 期权链为空？
**A**: 确认标的有期权交易，检查是否在交易时间，尝试不同的 strikeCount。

### Q4: 如何获取历史数据？
**A**: 当前版本专注于实时数据，历史数据功能计划在后续版本添加。

## 📚 更多资源

- [完整集成指南](SCHWAB_INTEGRATION_GUIDE.md) - 详细文档
- [功能总结](SCHWAB_FEATURE_SUMMARY.md) - 功能清单
- [Schwab API 文档](https://developer.schwab.com/) - 官方文档

## 🎓 学习路径

1. **入门** (30 分钟)
   - 配置凭据
   - 运行基础测试
   - 理解数据模型

2. **进阶** (1-2 小时)
   - 持仓分析
   - 期权链研究
   - 策略开发

3. **高级** (持续学习)
   - 自动化交易
   - 风险管理
   - 策略回测

---

**开始时间**: 现在  
**预计完成**: 10 分钟  
**难度**: ⭐⭐☆☆☆ (简单)

祝你使用愉快！🚀
