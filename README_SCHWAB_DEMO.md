# Schwab 登录演示说明

## 🎯 快速演示

我已经为你创建了 Schwab OAuth 登录功能的完整演示。有两种方式查看：

### 方式 1：文字版演示（无需配置）

直接运行 PowerShell 脚本查看登录界面的文字版演示：

```powershell
powershell -File ShowSchwabLogin.ps1
```

这会显示：
- ✅ Schwab 登录页面的文字版界面
- ✅ OAuth 授权页面的文字版界面
- ✅ 如何配置和启动真实流程的说明

### 方式 2：真实浏览器演示（需要 Schwab App Key）

如果你有 Schwab 开发者账号，可以看到真实的浏览器登录：

1. **配置 App Key**：
   ```bash
   cd src/Quant.Infra.Net.Console
   dotnet user-secrets set "Schwab:AppKey" "your-app-key"
   ```

2. **运行程序**：
   ```bash
   dotnet run
   ```

3. **选择菜单**：
   - 输入 `1` - 进入 Schwab OAuth 演示
   - 输入 `1` - 查看登录界面预览（文字版）
   - 输入 `2` - 启动真实 OAuth 流程（会打开浏览器）

4. **浏览器会自动打开**，显示真实的 Schwab 登录页面！

## 📋 已实现的功能

### ✅ OAuth 认证
- 自动打开浏览器到 Schwab 登录页面
- 本地 HTTP 服务器监听回调（localhost:8080）
- 自动获取授权码
- 美观的成功/失败页面

### ✅ 账户管理
- 获取账户信息（余额、市值、购买力）
- 查看所有持仓
- 查询单个持仓

### ✅ 市场数据
- 获取股票实时报价
- 批量获取报价
- **期权链数据**（Call/Put、希腊字母、隐含波动率）

### ✅ 交易功能
- 下单（市价单、限价单、止损单）
- 查询订单状态
- 取消订单

## 📚 文档

- `SCHWAB_DEVELOPER_REGISTRATION_GUIDE.md` - 如何申请开发者账号
- `SCHWAB_QUICKSTART.md` - 5分钟快速上手
- `SCHWAB_INTEGRATION_GUIDE.md` - 完整集成文档
- `SCHWAB_FEATURE_SUMMARY.md` - 功能总结

## 🔧 技术实现

### 文件结构
```
src/Quant.Infra.Net/
├── Broker/
│   ├── Interfaces/
│   │   └── ISchwabBrokerService.cs      # 接口定义
│   └── Service/
│       └── SchwabBrokerService.cs        # 完整实现（~600行）
│
src/Quant.Infra.Net.Console/
├── SchwabAuthDemo.cs                     # OAuth 认证演示
├── SchwabAuthDemoProgram.cs              # 演示程序主入口
└── Program.cs                            # 控制台主程序

ShowSchwabLogin.ps1                       # 文字版登录界面演示
DemoSchwabLogin.ps1                       # 自动演示脚本
```

### OAuth 流程

1. **构建授权 URL**
   ```
   https://api.schwabapi.com/v1/oauth/authorize
   ?client_id=YOUR_APP_KEY
   &redirect_uri=http://localhost:8080/callback
   &response_type=code
   ```

2. **打开浏览器** - 自动检测操作系统（Windows/Linux/macOS）

3. **本地服务器监听** - HttpListener 在 localhost:8080 等待回调

4. **获取授权码** - 从回调 URL 的查询参数中提取

5. **交换令牌** - 使用授权码获取访问令牌（在 SchwabBrokerService 中实现）

## 🎨 登录界面预览

运行演示脚本后，你会看到类似这样的界面：

```
+------------------------------------------------------------------------------+
| Charles Schwab - Login                                                        |
+==============================================================================+
|                                                                               |
|   [Schwab Logo]                                                               |
|                                                                               |
|   Login to Your Account                                                       |
|                                                                               |
|   +-------------------------------------------------------------------+       |
|   | Username or Account Number                                        |       |
|   +-------------------------------------------------------------------+       |
|                                                                               |
|   +-------------------------------------------------------------------+       |
|   | Password                                                          |       |
|   +-------------------------------------------------------------------+       |
|                                                                               |
|   [ ] Remember Me                                                             |
|                                                                               |
|   +-------------------------------------------------------------------+       |
|   |                         [  L O G I N  ]                           |       |
|   +-------------------------------------------------------------------+       |
|                                                                               |
|   Forgot Password?  |  Need Help?                                            |
|                                                                               |
+------------------------------------------------------------------------------+
```

## 🚀 下一步

1. **测试演示** - 运行 `ShowSchwabLogin.ps1` 查看文字版
2. **申请开发者账号** - 参考 `SCHWAB_DEVELOPER_REGISTRATION_GUIDE.md`
3. **配置凭据** - 设置 App Key 和 Secret
4. **运行真实流程** - 看到浏览器自动打开
5. **开始交易** - 使用 API 获取数据和下单

## ❓ 常见问题

**Q: 为什么浏览器没有自动打开？**  
A: 检查是否有 App Key，或者手动复制 URL 到浏览器。

**Q: 端口 8080 被占用怎么办？**  
A: 修改 `SchwabAuthDemo.cs` 中的 `redirectUri` 参数。

**Q: 如何获取 Schwab 开发者账号？**  
A: 查看 `SCHWAB_DEVELOPER_REGISTRATION_GUIDE.md` 详细步骤。

**Q: 可以用模拟账户测试吗？**  
A: 可以，Schwab 提供沙盒环境用于测试。

---

**当前分支**: `feature/schwab-integration`  
**状态**: ✅ 功能完整，可以演示  
**下一步**: 提交代码或创建 PR
