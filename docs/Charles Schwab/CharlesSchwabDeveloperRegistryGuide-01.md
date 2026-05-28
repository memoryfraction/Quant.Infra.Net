# Charles Schwab Developer Registration Guide / Charles Schwab Developer 注册指南

> **Quant.Infra.Net** — A step-by-step guide to apply for and configure a Charles Schwab Developer account.
> 一份手把手的 Charles Schwab Developer 账户申请与配置指南。

---

## Version History / 版本历史

| Version / 版本 | Date / 日期 | Description / 描述 |
|------|------|-------------|
| 1.0.0 | 2026-05-28 | Consolidated release: combines developer registration, application creation, credential management, and callback URL configuration / 合并发布：整合开发者注册、应用创建、凭据管理与回调 URL 配置 |

---

# English

## Prerequisites

Before you begin, make sure you have the following:

- An active **Charles Schwab trading account** (open one at [schwab.com](https://www.schwab.com/) first if you don't have one)
- A valid **email address**
- A **US phone number** for verification (Charles Schwab may change this requirement; always check the latest official policy)

> **Note:** One developer account can be used for multiple Charles Schwab trading accounts. You do not need to register separately for each trading account.

---

## Step 1: Register a Developer Account

1. Open your browser and go to **[Charles Schwab Developer Portal](https://developer.schwab.com/)**
2. Click **"Get Started"** or **"Sign In"** to begin
3. Log in with your **Charles Schwab trading account credentials** (your Schwab online banking username and password)
4. Fill in the developer information as prompted and submit your application

![Charles Developer Registry](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Charles%20Developer%20Registry.jpg?raw=true)

**Important:**
- The developer account is **different from your trading account** — do not confuse them. The developer account manages API applications, while the trading account handles actual trading.
- A US phone number is currently required for verification.

---

## Step 2: Wait for Approval

- After submitting a complete registration, you will typically receive a response within **1–3 business days**
- Once approved, your developer account gains API access
- You will receive an email notification when your application is approved

---

## Step 3: Create an Application and Configure Redirect URI

Once your developer account is approved, you need to create an application:

1. Log in to the [Developer Portal](https://developer.schwab.com/)
2. Navigate to **"My Apps"** or **"Applications"**
3. Click **"Create New App"** or **"Register Application"**
4. Fill in the application information:
   - **App Name:** e.g., `Quant Trading System`
   - **App Description:** Describe your usage (e.g., "Quantitative trading system for market data, portfolio management, and strategy execution")
   - **App Type:** `Individual Trader API`
   - **Callback URL (Redirect URI):** `https://127.0.0.1:8443/`
5. Select the required API permissions:
   - Account Information
   - Trading
   - Market Data
   - Options
6. Submit the application and wait for approval (typically 1-2 business days)

### About the Redirect URI Port

- **The recommended port is `8443`**, but you may use any available port
- The port registered in the developer portal's callback URL **must exactly match** the `RedirectUri` setting in your application configuration
- Example: if you use port `8443`, set: `https://127.0.0.1:8443/`

After the application is approved, your application status will show **"Ready to Use"**:

![Charles Developer Registry Ready](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Charles%20Developer%20Registry%20Ready.jpg?raw=true)

---

## Step 4: Obtain Your Credentials

Once the application is approved, you will receive:

| Credential | Description |
|------------|-------------|
| **Client ID (App Key)** | Your application's public identifier |
| **Client Secret (App Secret)** | Your application's secret key — **only shown once** |

> **Warning:** The Client Secret is only displayed **once** at the time of creation. Save it immediately and securely. If lost, you must regenerate it in the developer portal.

---

## Step 5: Store Credentials Securely

**Do NOT commit credentials to source control.** Use one of these methods:

### Method 1: .NET User Secrets (Recommended for Development)

```bash
# Initialize User Secrets for your project
dotnet user-secrets init

# Set Schwab credentials
dotnet user-secrets set "Schwab:ClientId" "your-client-id"
dotnet user-secrets set "Schwab:ClientSecret" "your-client-secret"
dotnet user-secrets set "Schwab:AccountNumber" "your-account-number"
```

### Method 2: Environment Variables

```bash
# Windows (PowerShell)
setx SCHWAB_CLIENT_ID "your-client-id"
setx SCHWAB_CLIENT_SECRET "your-client-secret"
setx SCHWAB_ACCOUNT_NUMBER "your-account-number"
```

### Method 3: appsettings.json (Development Only)

```json
{
  "Schwab": {
    "OAuth": {
      "RedirectUri": "https://localhost:8443/callback",
      "ClientId": "",
      "ClientSecret": ""
    },
    "AccountNumber": ""
  }
}
```

> **Note:** If you must use `appsettings.json`, leave `ClientId` and `ClientSecret` empty and use User Secrets or environment variables instead.

### Git .gitignore

Add these entries to your `.gitignore`:

```
appsettings.Development.json
appsettings.Production.json
*.secrets.json
```

---

## Configuration Checklist

- [ ] Charles Schwab trading account opened and verified
- [ ] Developer account registered at [developer.schwab.com](https://developer.schwab.com/)
- [ ] Developer account status is **"Ready to Use"** / **"Approved"**
- [ ] Client ID (App Key) and Client Secret obtained
- [ ] Redirect URI registered in developer portal (port matches application configuration)
- [ ] Credentials securely stored (User Secrets / environment variables)
- [ ] `RedirectUri` matches exactly between developer portal and appsettings.json

---

## Estimated Timeline

| Step | Estimated Time |
|------|---------------|
| Open Schwab trading account | 1-3 days |
| Register developer account | Instant |
| Create application | 5-10 minutes |
| Get API credentials | Instant (after approval) |
| App approval | 1-2 business days |
| **Total** | **2-5 days** |

---

## Notes

- **Policy Changes**: Charles Schwab's developer registration policy may change at any time (e.g., phone number requirements, review duration). Always refer to [developer.schwab.com](https://developer.schwab.com/) for the latest official information.
- **Security**: A leaked Client Secret could allow others to use your API permissions — store it carefully and never share it.
- **Compliance**: API usage must comply with the Charles Schwab developer agreement and all applicable laws and regulations.
- **Callback URL**: The callback URL in the developer portal **must be `https://127.0.0.1:PORT/`** (or `https://localhost:PORT/callback` depending on your project). Trailing slash matters in some configurations.

---

# 中文版

## 前置准备

在开始之前，请确保你已经具备以下条件：

- **有效的 Charles Schwab 交易账户**（如无账户，请先前往 [schwab.com](https://www.schwab.com/) 开户）
- **有效的电子邮箱地址**
- **用于验证的美国手机号**（Charles Schwab 可能随时调整此要求，请以官方最新政策为准）

> **注意：** 一个开发者账号可用于多个 Charles Schwab 交易账户，无需为每个交易账户单独注册。

---

## 步骤 1：注册 Developer 账户

1. 打开浏览器访问 **[Charles Schwab Developer Portal](https://developer.schwab.com/)**
2. 点击 **"Get Started"** 或 **"Sign In"** 开始注册
3. 使用你的 **Charles Schwab 交易账户凭据**（网上银行的用户名和密码）登录
4. 根据页面提示填写开发者信息并提交申请

![Charles Developer Registry](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Charles%20Developer%20Registry.jpg?raw=true)

**重要说明：**
- **开发者账号与交易账号不同**，请勿混淆。开发者账号用于管理 API 应用，交易账号用于实际操作
- 当前政策要求必须使用**美国手机号**进行验证

---

## 步骤 2：等待审核

- 提交完整注册信息后，一般 **1-3 个工作日** 会收到审核结果通知
- 审核通过后，你的开发者账户将获得 API 访问权限
- 审核结果会通过邮件通知你

---

## 步骤 3：创建应用并配置回调 URL

开发者账户审核通过后，需要创建应用：

1. 登录 [Developer Portal](https://developer.schwab.com/)
2. 进入 **"My Apps"** 或 **"Applications"** 页面
3. 点击 **"Create New App"** 或 **"Register Application"**
4. 填写应用信息：
   - **应用名称：** 例如 `Quant Trading System`
   - **应用描述：** 说明使用场景（如："量化交易系统，用于获取市场数据、管理持仓和执行交易策略"）
   - **应用类型：** `Individual Trader API`
   - **回调 URL（Redirect URI）：** `https://127.0.0.1:8443/`
5. 勾选需要的 API 权限：
   - 账户信息（Account Information）
   - 交易功能（Trading）
   - 市场数据（Market Data）
   - 期权数据（Options）
6. 提交申请，等待审核（通常 1-2 个工作日）

### 关于回调 URL 端口

- **建议使用 `8443` 端口**，但你可以使用任意可用端口
- 在开发者门户注册的回调 URL 中的端口号**必须与应用配置中的 `RedirectUri` 完全一致**
- 例如：使用 `8443` 端口，则设置为 `https://127.0.0.1:8443/`

应用审核通过后，状态将显示为 **"Ready to Use"**：

![Charles Developer Registry Ready](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Charles%20Developer%20Registry%20Ready.jpg?raw=true)

---

## 步骤 4：获取凭据

应用审核通过后，你将获得以下凭据：

| 凭据 | 说明 |
|------|------|
| **Client ID（App Key）** | 应用的公共标识符 |
| **Client Secret（App Secret）** | 应用密钥 — **仅在创建时显示一次** |

> **警告：** Client Secret **仅在创建时显示一次**，请务必立即安全保存。如果遗失，需要在开发者门户中重新生成。

---

## 步骤 5：安全存储凭据

**禁止将凭据提交到代码仓库。** 建议使用以下方式之一：

### 方式一：.NET User Secrets（推荐用于开发环境）

```bash
# 初始化 User Secrets
dotnet user-secrets init

# 设置 Schwab 凭据
dotnet user-secrets set "Schwab:ClientId" "你的ClientId"
dotnet user-secrets set "Schwab:ClientSecret" "你的ClientSecret"
dotnet user-secrets set "Schwab:AccountNumber" "你的账户号码"
```

### 方式二：环境变量

```bash
# Windows (PowerShell)
setx SCHWAB_CLIENT_ID "你的ClientId"
setx SCHWAB_CLIENT_SECRET "你的ClientSecret"
setx SCHWAB_ACCOUNT_NUMBER "你的账户号码"
```

### 方式三：appsettings.json（仅开发用）

```json
{
  "Schwab": {
    "OAuth": {
      "RedirectUri": "https://localhost:8443/callback",
      "ClientId": "",
      "ClientSecret": ""
    },
    "AccountNumber": ""
  }
}
```

> **注意：** 如果必须使用 `appsettings.json`，建议将 `ClientId` 和 `ClientSecret` 留空，改用 User Secrets 或环境变量配置。

### .gitignore 配置

在 `.gitignore` 中添加：

```
appsettings.Development.json
appsettings.Production.json
*.secrets.json
```

---

## 配置检查清单

- [ ] 已开设并验证 Schwab 交易账户
- [ ] 已在 [developer.schwab.com](https://developer.schwab.com/) 注册开发者账户
- [ ] 开发者账户状态为 **"Ready to Use"** / **"Approved"**
- [ ] 已获取 Client ID（App Key）和 Client Secret
- [ ] 已在开发者门户注册 Redirect URI（端口与应用配置一致）
- [ ] 凭据已安全存储（User Secrets / 环境变量）
- [ ] 开发者门户的 Redirect URI 与 appsettings.json 完全一致

---

## 预计时间线

| 步骤 | 预计时间 |
|------|---------|
| 开设 Schwab 交易账户 | 1-3 天 |
| 注册开发者账户 | 即时 |
| 创建应用 | 5-10 分钟 |
| 获取 API 凭据 | 即时（审核通过后） |
| 应用审核 | 1-2 个工作日 |
| **总计** | **2-5 天** |

---

## 注意事项

- **政策变更**：Charles Schwab 的开发者注册政策可能随时调整（如手机号要求、审核时长等），请以 [developer.schwab.com](https://developer.schwab.com/) 官方信息为准
- **安全问题**：Client Secret 泄露可能导致他人使用你的 API 权限，务必妥善保管，切勿分享
- **合规要求**：API 使用须遵守 Charles Schwab 开发者协议和相关法律法规
- **回调 URL**：开发者门户中的回调 URL **必须使用 `https://127.0.0.1:端口/`**（或根据项目使用 `https://localhost:端口/callback`）。某些配置中末尾斜杠很重要

---

> **Disclaimer**: See [DISCLAIMER.md](../DISCLAIMER.md) for full disclaimer and limitation of liability / 详见 [免责声明](../DISCLAIMER.md) 了解完整免责条款与责任限制。

---

**更新日期**: 2026-05-28
**版本**: 1.0.0
**维护者**: Quant.Infra.Net Team
