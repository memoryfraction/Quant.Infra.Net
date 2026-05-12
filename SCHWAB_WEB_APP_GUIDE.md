# 🌐 Schwab Web 应用使用指南

## 📱 功能概览

这是一个**完全基于浏览器**的 Schwab 交易平台，所有操作都在浏览器中完成：

### ✅ 已实现功能

1. **🔐 Web 登录界面**
   - 在浏览器中输入 App Key、Secret 和账户号码
   - 支持 OAuth 2.0 授权登录
   - 安全的 Session 管理

2. **📊 账户概览**
   - 总资产、现金余额、市值、购买力
   - 未实现/已实现盈亏
   - 账户详细信息

3. **💼 持仓管理**
   - 查看所有持仓
   - 实时盈亏显示
   - 持仓详情（数量、成本价、类型）

4. **📈 实时报价**
   - 输入股票代码查询实时报价
   - 显示买价/卖价、涨跌幅
   - 今日最高/最低价、成交量

5. **🎯 期权链**
   - 查询任意标的的期权链
   - Call/Put 期权分类显示
   - 希腊字母（Delta、Gamma、Theta、Vega、Rho）
   - 隐含波动率、未平仓合约
   - 价内期权高亮显示

6. **📋 订单管理**
   - 订单历史查看（开发中）

## 🚀 快速启动

### 方式 1：使用启动脚本（推荐）

```powershell
powershell -File StartSchwabWeb.ps1
```

### 方式 2：手动启动

```bash
cd src/Quant.Infra.Net.Web
dotnet run
```

启动后，浏览器会自动打开 `https://localhost:5001`

## 🔧 配置步骤

### 1. 获取 Schwab 开发者凭据

参考 `SCHWAB_DEVELOPER_REGISTRATION_GUIDE.md` 申请开发者账号

### 2. 配置用户机密（可选）

如果你想预先配置凭据，可以使用 user secrets：

```bash
cd src/Quant.Infra.Net.Web
dotnet user-secrets init
dotnet user-secrets set "Schwab:AppKey" "your-app-key"
dotnet user-secrets set "Schwab:Secret" "your-app-secret"
```

### 3. 启动应用

```bash
dotnet run
```

### 4. 在浏览器中登录

1. 打开 `https://localhost:5001`
2. 输入你的 Schwab 凭据：
   - App Key
   - App Secret
   - 账户号码
3. 点击"登录并授权"

## 📸 界面预览

### 登录页面
```
┌─────────────────────────────────────────┐
│     🔐 Charles Schwab 登录              │
├─────────────────────────────────────────┤
│                                         │
│  App Key:     [________________]        │
│  App Secret:  [________________]        │
│  账户号码:     [________________]        │
│                                         │
│         [ 登录并授权 ]                   │
│                                         │
│  ─────────── 或 ───────────             │
│                                         │
│    [ 使用 OAuth 2.0 登录 ]              │
│                                         │
└─────────────────────────────────────────┘
```

### 仪表板
```
┌─────────────────────────────────────────────────────────────┐
│  📊 Schwab Trading Dashboard              [ 退出登录 ]      │
├─────────────────────────────────────────────────────────────┤
│  [ 账户概览 ] [ 持仓管理 ] [ 实时报价 ] [ 期权链 ] [ 订单 ] │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │
│  │ 总资产    │  │ 现金余额  │  │ 市值      │  │ 购买力    │  │
│  │ $125,000 │  │ $25,000  │  │ $100,000 │  │ $50,000  │  │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │
│                                                             │
│  持仓列表:                                                  │
│  ┌───────────────────────────────────────────────────┐    │
│  │ 标的  │ 数量 │ 成本价  │ 类型    │ 未实现盈亏      │    │
│  ├───────────────────────────────────────────────────┤    │
│  │ AAPL  │ 100  │ $150.00 │ 股票    │ +$2,500.00     │    │
│  │ MSFT  │ 50   │ $300.00 │ 股票    │ +$1,200.00     │    │
│  └───────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## 🎨 功能特点

### 1. 美观的 UI 设计
- 渐变色背景
- 卡片式布局
- 响应式设计（支持手机、平板、电脑）
- 平滑的动画效果

### 2. 实时数据更新
- 账户信息实时刷新
- 持仓盈亏实时计算
- 报价数据实时查询

### 3. 智能数据展示
- 盈利显示绿色，亏损显示红色
- 价内期权高亮显示
- 大数字格式化（千分位分隔）

### 4. 安全性
- HTTPS 加密传输
- Session 管理
- 30 分钟自动超时

## 📋 使用场景

### 场景 1：查看账户和持仓

1. 登录后自动显示账户概览
2. 点击"持仓管理"查看所有持仓
3. 实时查看盈亏情况

### 场景 2：查询股票报价

1. 点击"实时报价"标签
2. 输入股票代码（如 AAPL）
3. 查看实时价格、涨跌幅、成交量

### 场景 3：分析期权链

1. 点击"期权链"标签
2. 输入标的代码（如 AAPL）
3. 查看 Call/Put 期权
4. 分析希腊字母和隐含波动率
5. 价内期权会高亮显示

### 场景 4：期权策略分析

```
示例：查找高 Delta Call 期权
1. 输入标的代码
2. 查看 Call 期权表
3. 筛选 Delta > 0.7 的期权
4. 查看成交量和未平仓合约
5. 选择合适的期权进行交易
```

## 🔧 技术架构

### 前端
- **Razor Pages** - ASP.NET Core 视图引擎
- **纯 CSS** - 无需额外框架
- **原生 JavaScript** - 标签切换和交互

### 后端
- **ASP.NET Core 8.0** - Web 框架
- **Session 管理** - 用户状态保持
- **依赖注入** - SchwabBrokerService

### 数据流
```
浏览器 → Razor Pages → SchwabBrokerService → Schwab API
   ↑                                              ↓
   └──────────────── JSON 响应 ←──────────────────┘
```

## 📁 项目结构

```
src/Quant.Infra.Net.Web/
├── Pages/
│   ├── Index.cshtml              # 登录页面
│   ├── Index.cshtml.cs           # 登录逻辑
│   ├── Dashboard.cshtml          # 仪表板页面
│   └── Dashboard.cshtml.cs       # 仪表板逻辑
├── Program.cs                    # 应用启动配置
└── Quant.Infra.Net.Web.csproj   # 项目文件

StartSchwabWeb.ps1                # 启动脚本
```

## ⚙️ 配置选项

### appsettings.json

```json
{
  "Schwab": {
    "AppKey": "your-app-key",
    "Secret": "your-app-secret"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### User Secrets（推荐）

```bash
dotnet user-secrets set "Schwab:AppKey" "your-app-key"
dotnet user-secrets set "Schwab:Secret" "your-app-secret"
```

## 🐛 故障排除

### Q1: 端口被占用
**A**: 修改 `Properties/launchSettings.json` 中的端口号

### Q2: 无法连接到 Schwab API
**A**: 检查 App Key 和 Secret 是否正确，确认网络连接

### Q3: Session 超时
**A**: 重新登录即可，Session 默认 30 分钟超时

### Q4: 期权链数据为空
**A**: 确认标的有期权交易，检查是否在交易时间

## 🚀 下一步开发

- [ ] 订单下单功能
- [ ] 订单历史查看
- [ ] 实时价格推送（WebSocket）
- [ ] 图表展示（K线图、期权链可视化）
- [ ] 策略回测
- [ ] 风险管理工具

## 📚 相关文档

- `SCHWAB_DEVELOPER_REGISTRATION_GUIDE.md` - 开发者注册指南
- `SCHWAB_QUICKSTART.md` - 快速开始
- `SCHWAB_INTEGRATION_GUIDE.md` - 完整集成文档
- `SCHWAB_FEATURE_SUMMARY.md` - 功能总结

## 🎉 开始使用

```bash
# 1. 启动应用
powershell -File StartSchwabWeb.ps1

# 2. 打开浏览器
# 访问 https://localhost:5001

# 3. 登录并开始交易！
```

---

**当前分支**: `feature/schwab-integration`  
**状态**: ✅ Web 应用已完成  
**访问地址**: `https://localhost:5001`

祝你交易愉快！📈
