# Charles Schwab 开发者账户申请指南

## 📋 申请流程概览

Charles Schwab 在 2023 年收购了 TD Ameritrade，目前正在整合两家公司的 API 服务。以下是详细的申请步骤。

## 🔍 重要说明

### 当前状态（2024-2026）
- ✅ **Schwab Trader API** - 已正式发布
- ✅ **个人交易账户 API** - 可用
- ⚠️ **机构 API** - 需要特殊申请
- 📝 **原 TD Ameritrade API** - 正在迁移到 Schwab

## 📝 申请步骤

### 步骤 1：准备工作

#### 1.1 需要的材料
- ✅ 有效的 Charles Schwab 交易账户
- ✅ 电子邮箱地址
- ✅ 应用程序描述
- ✅ 回调 URL（用于 OAuth）

#### 1.2 账户要求
- 必须有活跃的 Schwab 个人交易账户
- 账户需要完成身份验证
- 建议账户有一定的交易历史

### 步骤 2：访问开发者门户

#### 2.1 访问网站
1. 打开浏览器访问：[https://developer.schwab.com/](https://developer.schwab.com/)
2. 点击右上角的 **"Sign In"** 或 **"Get Started"**

#### 2.2 注册开发者账户
1. 如果已有 Schwab 账户，使用现有凭据登录
2. 如果没有，需要先注册 Schwab 交易账户：
   - 访问 [https://www.schwab.com/](https://www.schwab.com/)
   - 点击 "Open an Account"
   - 完成开户流程（需要 SSN、地址等信息）

### 步骤 3：创建应用程序

#### 3.1 登录开发者门户
1. 使用 Schwab 账户凭据登录
2. 进入 **"My Apps"** 或 **"Applications"** 页面

#### 3.2 创建新应用
1. 点击 **"Create New App"** 或 **"Register Application"**
2. 填写应用信息：

```
应用名称: Quant Trading System
应用描述: 量化交易系统，用于获取市场数据、管理持仓和执行交易策略
应用类型: Individual Trader API
回调 URL: https://localhost:8080/callback (开发环境)
```

#### 3.3 选择 API 权限
勾选需要的权限：
- ✅ **Account Information** - 账户信息
- ✅ **Trading** - 交易功能
- ✅ **Market Data** - 市场数据
- ✅ **Options** - 期权数据

### 步骤 4：获取 API 凭据

#### 4.1 生成凭据
1. 应用创建成功后，系统会生成：
   - **App Key (Client ID)** - 应用密钥
   - **App Secret (Client Secret)** - 应用机密
2. **重要**：立即保存这些凭据，Secret 只显示一次！

#### 4.2 记录信息
```
App Key: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
App Secret: yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
Account Number: 12345678
```

### 步骤 5：配置 OAuth 2.0

#### 5.1 理解认证流程
Schwab 使用 OAuth 2.0 授权码流程：
```
1. 用户授权 → 2. 获取授权码 → 3. 交换访问令牌 → 4. 使用令牌访问 API
```

#### 5.2 获取授权码
1. 构建授权 URL：
```
https://api.schwabapi.com/v1/oauth/authorize?
  client_id=YOUR_APP_KEY&
  redirect_uri=YOUR_CALLBACK_URL&
  response_type=code
```

2. 在浏览器中访问该 URL
3. 登录并授权应用
4. 系统会重定向到回调 URL，并附带授权码

#### 5.3 交换访问令牌
使用授权码获取访问令牌：
```bash
curl -X POST https://api.schwabapi.com/v1/oauth/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=authorization_code" \
  -d "code=YOUR_AUTH_CODE" \
  -d "client_id=YOUR_APP_KEY" \
  -d "client_secret=YOUR_APP_SECRET" \
  -d "redirect_uri=YOUR_CALLBACK_URL"
```

### 步骤 6：测试 API 访问

#### 6.1 使用 Postman 测试
1. 下载并安装 [Postman](https://www.postman.com/)
2. 导入 Schwab API 集合（如果有）
3. 配置环境变量：
   - `base_url`: https://api.schwabapi.com/trader/v1
   - `access_token`: 你的访问令牌

#### 6.2 测试基本请求
```bash
# 获取账户信息
curl -X GET "https://api.schwabapi.com/trader/v1/accounts/{accountNumber}" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# 获取报价
curl -X GET "https://api.schwabapi.com/marketdata/v1/quotes?symbols=AAPL" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## 🔐 安全最佳实践

### 1. 凭据管理
```bash
# 使用环境变量
export SCHWAB_APP_KEY="your-app-key"
export SCHWAB_APP_SECRET="your-app-secret"
export SCHWAB_ACCOUNT_NUMBER="your-account-number"

# 或使用 .NET User Secrets
dotnet user-secrets set "Schwab:ApiKey" "your-app-key"
dotnet user-secrets set "Schwab:Secret" "your-app-secret"
dotnet user-secrets set "Schwab:AccountNumber" "your-account-number"
```

### 2. 不要提交到代码仓库
在 `.gitignore` 中添加：
```
# Schwab API 凭据
appsettings.Development.json
appsettings.Production.json
*.secrets.json
```

### 3. 使用 HTTPS
- 所有 API 请求必须使用 HTTPS
- 回调 URL 在生产环境必须使用 HTTPS

## 📊 API 限制和配额

### 免费账户限制
- **请求频率**：每秒 120 次请求
- **每日配额**：通常无限制（合理使用）
- **令牌有效期**：30 分钟（可刷新）
- **刷新令牌有效期**：7 天

### 数据延迟
- **实时数据**：需要专业订阅
- **延迟数据**：免费账户通常有 15 分钟延迟
- **期权数据**：实时可用

## 🆘 常见问题

### Q1: 我没有 Schwab 账户，可以申请开发者账户吗？
**A**: 不可以。必须先开设 Schwab 交易账户才能申请开发者访问权限。

### Q2: 开户需要什么条件？
**A**: 
- 年满 18 岁
- 有效的 SSN（美国社会安全号）或 ITIN
- 美国地址
- 最低存款要求（通常 $0，但建议至少 $1000）

### Q3: 非美国居民可以申请吗？
**A**: 
- 部分国家的居民可以开设国际账户
- 需要提供护照和地址证明
- API 访问权限可能有限制
- 建议联系 Schwab 国际部门确认

### Q4: API 是免费的吗？
**A**: 
- 基础 API 访问免费
- 实时市场数据可能需要订阅
- 交易功能免费（但有交易佣金）

### Q5: 审核需要多长时间？
**A**: 
- 开发者账户：即时批准
- 应用注册：通常 1-2 个工作日
- 生产环境访问：可能需要额外审核

### Q6: 可以用于生产环境吗？
**A**: 
- 是的，API 已正式发布
- 建议先在测试环境充分测试
- 注意风险管理和错误处理

## 🔄 从 TD Ameritrade 迁移

### 如果你有 TD Ameritrade 开发者账户

#### 迁移步骤
1. 访问 [https://developer.schwab.com/](https://developer.schwab.com/)
2. 使用 TD Ameritrade 凭据登录
3. 系统会引导你完成迁移流程
4. 更新应用配置和 API 端点

#### API 端点变化
```
旧端点: https://api.tdameritrade.com/v1/
新端点: https://api.schwabapi.com/trader/v1/
```

#### 代码更新
```csharp
// 旧代码
var baseUrl = "https://api.tdameritrade.com/v1/";

// 新代码
var baseUrl = "https://api.schwabapi.com/trader/v1/";
```

## 📞 获取帮助

### 官方支持渠道
1. **开发者门户**：[https://developer.schwab.com/](https://developer.schwab.com/)
2. **文档中心**：[https://developer.schwab.com/products/trader-api--individual/details/documentation](https://developer.schwab.com/products/trader-api--individual/details/documentation)
3. **支持邮箱**：api@schwab.com
4. **客服电话**：1-800-435-4000（选择技术支持）

### 社区资源
1. **Reddit**: r/algotrading
2. **Stack Overflow**: 标签 `schwab-api`
3. **GitHub**: 搜索 Schwab API 相关项目

## 📚 推荐阅读

### 官方文档
- [Schwab Trader API 概览](https://developer.schwab.com/products/trader-api--individual)
- [OAuth 2.0 认证指南](https://developer.schwab.com/products/trader-api--individual/details/documentation/Retail%20Trader%20API%20Production)
- [API 参考文档](https://developer.schwab.com/products/trader-api--individual/details/specifications/Retail%20Trader%20API%20Production)

### 第三方教程
- [Schwab API Python 教程](https://github.com/topics/schwab-api)
- [量化交易入门指南](https://www.quantstart.com/)

## ✅ 申请检查清单

在开始之前，确保你已经：
- [ ] 开设了 Schwab 交易账户
- [ ] 完成了账户验证
- [ ] 准备好应用描述
- [ ] 了解 OAuth 2.0 流程
- [ ] 设置好开发环境
- [ ] 阅读了 API 文档
- [ ] 准备好测试计划

## 🎯 预计时间线

| 步骤 | 预计时间 |
|------|----------|
| 开设 Schwab 账户 | 1-3 天 |
| 注册开发者账户 | 即时 |
| 创建应用程序 | 5-10 分钟 |
| 获取 API 凭据 | 即时 |
| 配置 OAuth | 30 分钟 |
| 测试 API | 1-2 小时 |
| **总计** | **2-5 天** |

## 🚀 下一步

完成申请后：
1. 阅读 [SCHWAB_QUICKSTART.md](SCHWAB_QUICKSTART.md) 快速开始
2. 查看 [SCHWAB_INTEGRATION_GUIDE.md](SCHWAB_INTEGRATION_GUIDE.md) 详细文档
3. 运行测试用例验证集成
4. 开始构建你的量化交易系统！

---

**更新日期**: 2026-04-19  
**版本**: 1.0.0  
**维护者**: Quant.Infra.Net Team

**免责声明**: 本指南仅供参考，具体申请流程以 Charles Schwab 官方网站为准。API 访问权限和功能可能随时变化，请以官方文档为准。
