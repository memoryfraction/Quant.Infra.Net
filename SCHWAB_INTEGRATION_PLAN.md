# 嘉信理财 API 集成计划

## 📋 项目概述

将 Charles Schwab（嘉信理财）Trader API 集成到 Saas.Infra.Net 架构中，支持：
- OAuth 2.0 认证
- 账户管理
- 市场数据查询
- 交易下单

## 🏗️ 架构设计

### 1. Core 层（已完成）
✅ **接口定义**：
- `ISchwabAuthService` - OAuth 认证服务
- `ISchwabAccountService` - 账户服务
- `ISchwabMarketDataService` - 市场数据服务
- `ISchwabTradingService` - 交易服务

✅ **领域模型**：
- `SchwabTokenResponse` - OAuth 令牌
- `SchwabAccountNumber` - 账户号码
- `SchwabAccount` - 账户详情
- `SchwabPosition` - 持仓
- `SchwabOrder` - 订单
- `SchwabQuote` - 报价
- `SchwabPriceHistory` - 历史价格
- `SchwabOrderRequest` - 订单请求

### 2. Data 层（已完成）
✅ **数据实体**：
- `SchwabTokenEntity` - 令牌存储
- `SchwabAccountEntity` - 账户存储

✅ **DbContext 更新**：
- 添加 `SchwabTokens` DbSet
- 添加 `SchwabAccounts` DbSet
- 配置实体映射关系

### 3. Services 层（待实现）
📝 **需要创建的服务**：

#### 3.1 SchwabAuthService
```csharp
src/Saas.Infra.Services/Schwab/SchwabAuthService.cs
```
**功能**：
- 生成 OAuth 授权 URL
- 交换授权码获取令牌
- 刷新访问令牌
- 管理令牌生命周期

**依赖**：
- `HttpClient` - HTTP 请求
- `ApplicationDbContext` - 数据库访问
- `IConfiguration` - 配置读取

#### 3.2 SchwabAccountService
```csharp
src/Saas.Infra.Services/Schwab/SchwabAccountService.cs
```
**功能**：
- 获取账户号码列表
- 获取账户详情
- 获取持仓信息
- 获取订单历史

#### 3.3 SchwabMarketDataService
```csharp
src/Saas.Infra.Services/Schwab/SchwabMarketDataService.cs
```
**功能**：
- 获取实时报价
- 获取批量报价
- 获取历史价格数据

#### 3.4 SchwabTradingService
```csharp
src/Saas.Infra.Services/Schwab/SchwabTradingService.cs
```
**功能**：
- 创建订单
- 取消订单
- 查询订单状态

#### 3.5 SchwabHttpClient（辅助类）
```csharp
src/Saas.Infra.Services/Schwab/SchwabHttpClient.cs
```
**功能**：
- 封装 HTTP 请求
- 自动添加 Authorization 头
- 处理 API 响应
- 错误处理和重试

### 4. MVC 层（待实现）
📝 **需要创建的控制器**：

#### 4.1 SchwabAuthController
```csharp
src/Saas.Infra.MVC/Controllers/SchwabAuthController.cs
```
**端点**：
- `GET /api/schwab/auth/authorize` - 获取授权 URL
- `GET /api/schwab/auth/callback` - OAuth 回调
- `POST /api/schwab/auth/refresh` - 刷新令牌
- `GET /api/schwab/auth/status` - 检查认证状态

#### 4.2 SchwabAccountController
```csharp
src/Saas.Infra.MVC/Controllers/SchwabAccountController.cs
```
**端点**：
- `GET /api/schwab/accounts` - 获取账户列表
- `GET /api/schwab/accounts/{hashValue}` - 获取账户详情
- `GET /api/schwab/accounts/{hashValue}/positions` - 获取持仓
- `GET /api/schwab/accounts/{hashValue}/orders` - 获取订单

#### 4.3 SchwabMarketDataController
```csharp
src/Saas.Infra.MVC/Controllers/SchwabMarketDataController.cs
```
**端点**：
- `GET /api/schwab/quotes/{symbol}` - 获取单个报价
- `POST /api/schwab/quotes` - 批量获取报价
- `GET /api/schwab/pricehistory/{symbol}` - 获取历史数据

#### 4.4 SchwabTradingController
```csharp
src/Saas.Infra.MVC/Controllers/SchwabTradingController.cs
```
**端点**：
- `POST /api/schwab/orders` - 创建订单
- `DELETE /api/schwab/orders/{orderId}` - 取消订单
- `GET /api/schwab/orders/{orderId}` - 获取订单详情

## 🔐 配置管理

### appsettings.json
```json
{
  "Schwab": {
    "ClientId": "",
    "ClientSecret": "",
    "RedirectUri": "https://127.0.0.1/schwab/callback",
    "BaseUrl": "https://api.schwabapi.com",
    "AuthorizationEndpoint": "https://api.schwabapi.com/v1/oauth/authorize",
    "TokenEndpoint": "https://api.schwabapi.com/v1/oauth/token"
  }
}
```

### User Secrets（开发环境）
```bash
dotnet user-secrets set "Schwab:ClientId" "your-client-id"
dotnet user-secrets set "Schwab:ClientSecret" "your-client-secret"
```

### 环境变量（生产环境）
```bash
SCHWAB_CLIENT_ID=your-client-id
SCHWAB_CLIENT_SECRET=your-client-secret
```

## 📊 数据库迁移

### 创建迁移
```bash
cd src/Saas.Infra.MVC
dotnet ef migrations add AddSchwabTables --project ../Saas.Infra.Data
```

### 应用迁移
```bash
dotnet ef database update --project ../Saas.Infra.Data
```

### SQL 脚本（手动创建）
```sql
-- Schwab Tokens 表
CREATE TABLE schwab_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES "Users"(Id) ON DELETE CASCADE,
    access_token VARCHAR(2000) NOT NULL,
    refresh_token VARCHAR(2000) NOT NULL,
    token_type VARCHAR(50) NOT NULL DEFAULT 'Bearer',
    expires_in INTEGER NOT NULL,
    scope VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT UQ_SchwabTokens_UserId UNIQUE (user_id)
);

-- Schwab Accounts 表
CREATE TABLE schwab_accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES "Users"(Id) ON DELETE CASCADE,
    account_number VARCHAR(100) NOT NULL,
    hash_value VARCHAR(200) NOT NULL,
    account_type VARCHAR(50),
    nickname VARCHAR(200),
    is_primary BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    CONSTRAINT UQ_SchwabAccounts_UserId_AccountNumber UNIQUE (user_id, account_number)
);

-- 创建索引
CREATE INDEX IX_SchwabTokens_UserId ON schwab_tokens(user_id);
CREATE INDEX IX_SchwabAccounts_UserId ON schwab_accounts(user_id);
```

## 🔄 OAuth 认证流程

### 1. 初始授权
```
用户 → 前端请求授权 URL
    → SchwabAuthController.GetAuthorizationUrl()
    → 重定向到 Schwab 登录页
    → 用户登录并授权
    → Schwab 重定向到 callback URL（带 code）
    → SchwabAuthController.Callback(code)
    → 交换 code 获取 access_token 和 refresh_token
    → 存储到数据库
```

### 2. 令牌刷新（每 29 分钟）
```
后台任务 → SchwabAuthService.RefreshAccessTokenAsync()
         → 使用 refresh_token 获取新的 access_token
         → 更新数据库
```

### 3. 令牌使用
```
API 请求 → 从数据库获取 access_token
        → 检查是否过期
        → 如果过期，先刷新
        → 添加到 Authorization 头
        → 发送请求
```

## 🧪 测试计划

### 单元测试
- `SchwabAuthServiceTests` - 认证服务测试
- `SchwabAccountServiceTests` - 账户服务测试
- `SchwabMarketDataServiceTests` - 市场数据测试
- `SchwabTradingServiceTests` - 交易服务测试

### 集成测试
- OAuth 流程端到端测试
- API 调用集成测试
- 令牌刷新测试

## 📝 依赖注入配置

### Program.cs
```csharp
// Schwab 配置
builder.Services.Configure<SchwabOptions>(
    builder.Configuration.GetSection("Schwab"));

// Schwab 服务注册
builder.Services.AddHttpClient<ISchwabAuthService, SchwabAuthService>();
builder.Services.AddScoped<ISchwabAccountService, SchwabAccountService>();
builder.Services.AddScoped<ISchwabMarketDataService, SchwabMarketDataService>();
builder.Services.AddScoped<ISchwabTradingService, SchwabTradingService>();
builder.Services.AddScoped<SchwabHttpClient>();

// 后台任务（令牌刷新）
builder.Services.AddHostedService<SchwabTokenRefreshService>();
```

## 🚀 实施步骤

### 阶段 1：基础设施（已完成）
- [x] Core 层接口和模型
- [x] Data 层实体和 DbContext

### 阶段 2：服务实现（进行中）
- [ ] SchwabHttpClient 辅助类
- [ ] SchwabAuthService 实现
- [ ] SchwabAccountService 实现
- [ ] SchwabMarketDataService 实现
- [ ] SchwabTradingService 实现

### 阶段 3：API 控制器
- [ ] SchwabAuthController
- [ ] SchwabAccountController
- [ ] SchwabMarketDataController
- [ ] SchwabTradingController

### 阶段 4：前端集成
- [ ] Blazor 组件
- [ ] OAuth 授权页面
- [ ] 账户管理页面
- [ ] 交易下单页面

### 阶段 5：测试和文档
- [ ] 单元测试
- [ ] 集成测试
- [ ] API 文档
- [ ] 用户手册

## 📚 参考资料

- [Charles Schwab Developer Portal](https://developer.schwab.com/)
- [Schwab API 非官方指南](https://medium.com/@carstensavage/the-unofficial-guide-to-charles-schwabs-trader-apis-14c1f5bc1d57)
- [OAuth 2.0 规范](https://oauth.net/2/)

## ⚠️ 注意事项

1. **安全性**：
   - 永远不要在代码中硬编码 ClientId 和 ClientSecret
   - 使用 User Secrets（开发）和环境变量（生产）
   - 令牌必须加密存储

2. **令牌管理**：
   - Access Token 有效期 30 分钟
   - Refresh Token 有效期 7 天
   - 需要实现自动刷新机制

3. **API 限制**：
   - 注意 Schwab API 的速率限制
   - 实现请求重试和退避策略

4. **账户哈希值**：
   - Schwab API 要求使用账户哈希值而非账户号码
   - 首次调用需要获取并存储哈希值

5. **合规性**：
   - 遵守证券交易相关法规
   - 实现适当的风险控制
   - 记录所有交易操作

## 🎯 下一步行动

1. 实现 `SchwabHttpClient` 辅助类
2. 实现 `SchwabAuthService` 认证服务
3. 创建数据库迁移
4. 实现 OAuth 回调控制器
5. 测试完整的认证流程

---

**创建时间**：2026-04-17  
**分支**：`features/schwab-api-integration`  
**状态**：进行中
