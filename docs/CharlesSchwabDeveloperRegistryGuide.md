# Charles Schwab Developer Registration Guide / Charles Schwab 开发者注册指南

> **Quant.Infra.Net** — A step-by-step guide to apply for and configure a Charles Schwab Developer account.

---

## Version History / 版本历史

| Version / 版本 | Date / 日期 | Description / 描述 |
|------|------|-------------|
| 1.0.0 | 2026-05-25 | Initial release: developer registration process, configuration essentials, and security notes / 初版发布：开发者注册流程、配置要点与安全说明 |

---

# English

## Registration Process

### Step 1: Register a Charles Schwab Developer Account

1. Open your browser and go to **[Charles Schwab Developer Portal](https://developer.schwab.com/)**
2. Click **"Get Started"** or **"Sign In"** to begin
3. Log in with your Charles Schwab trading account credentials (if you don't have a trading account, open one at [schwab.com](https://www.schwab.com/) first)
4. Fill in the developer information as prompted and submit your application

![Charles Developer Registry](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Developer%20Registry.jpg?raw=true)

**Important Notes:**
- **One developer account can be used for multiple Charles Schwab trading accounts** — you do not need to register separately for each trading account
- **The developer account is different from the trading account** — do not confuse them. The developer account manages API applications, while the trading account handles actual trading
- **A US phone number is currently required** for verification (Charles Schwab may change this policy at any time — always refer to the latest official requirements)

### Step 2: Wait for Approval

- After submitting a complete registration, you will typically receive a response within **1–3 business days**
- Once approved, your developer account gains API access

### Step 3: Configure Your Application and Port

- After activation, your application status in the developer portal will show **"Ready to Use"**
- When creating an application in the developer portal, **the recommended port for the callback URL (Redirect URI) is `8443`**
- You may use any port, provided that:
  - The port registered in the developer portal's callback URL
  - **Must match** the `Schwab.OAuth.RedirectUri` setting in `Quant.Infra.Net.Web`'s `appsettings.json`

![Charles Developer Registry Ready](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Developer%20Registry%20Ready.jpg?raw=true)

### Step 4: Obtain and Store Your Credentials

- After activation, you will receive a **Client ID (App Key)** and **Client Secret (App Secret)**
- **The Client Secret is only shown once** — save it immediately and securely
- Credentials must be **stored confidentially** and never committed to source control

#### Recommended Secure Storage Methods

```bash
# Method 1: .NET User Secrets (development environment)
dotnet user-secrets set "Schwab:ClientId" "your-client-id"
dotnet user-secrets set "Schwab:ClientSecret" "your-client-secret"

# Method 2: Environment Variables
setx SCHWAB_CLIENT_ID "your-client-id"
setx SCHWAB_CLIENT_SECRET "your-client-secret"
```

#### Configure `appsettings.json`

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

> **Note**: It is recommended to store `ClientId` and `ClientSecret` using User Secrets or environment variables rather than writing them in plain text in `appsettings.json`. The `RedirectUri` port must exactly match what is registered in the developer portal.

---

## Configuration Checklist

- [ ] Charles Schwab Developer account registered
- [ ] Developer account status is "Ready to Use"
- [ ] Client ID and Client Secret obtained
- [ ] Redirect URI port in developer portal matches application configuration
- [ ] Credentials securely stored (User Secrets / environment variables)
- [ ] `RedirectUri` updated in `appsettings.json`

---

## Notes

- **Policy Changes**: Charles Schwab's developer registration policy may change at any time (e.g., phone number requirements, review duration). Always refer to [developer.schwab.com](https://developer.schwab.com/) for the latest official information
- **Security**: A leaked Client Secret could allow others to use your API permissions — store it carefully
- **Compliance**: API usage must comply with the Charles Schwab developer agreement and all applicable laws and regulations

---

## Related Documents

- [Charles Schwab OAuth Ultimate Guide](SCHWAB_OAUTH_ULTIMATE_GUIDE.md)
- [Charles Schwab Quick Start](SCHWAB_QUICKSTART.md)
- [Charles Schwab Integration Guide](SCHWAB_INTEGRATION_GUIDE.md)

---

# 中文版

## 申请流程

### 步骤 1：注册 Charles Schwab Developer 账户

1. 打开浏览器访问 **[Charles Schwab Developer Portal](https://developer.schwab.com/)**
2. 点击 **"Get Started"** 或 **"Sign In"** 开始注册
3. 使用你的 Charles Schwab 交易账户凭据登录（如无交易账户，需先前往 [schwab.com](https://www.schwab.com/) 开户）
4. 根据页面提示填写开发者信息并提交申请

![Charles Developer Registry](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Developer%20Registry.jpg?raw=true)

**重要说明：**
- **一个开发者账号可用于多个 Charles Schwab 交易账户**，无需为每个交易账户单独注册开发者账号
- **开发者账号与账户操作账号不同**，请勿混淆。开发者账号用于管理 API 应用，账户操作账号用于交易
- **最新政策要求必须使用美国手机号**进行验证（Charles Schwab 官方随时可能调整政策，请以官方最新要求为准）

### 步骤 2：等待审核

- 提交完整注册信息后，一般 **1-3 个工作日** 会收到审核结果通知
- 审核通过后，你的开发者账户将获得 API 访问权限

### 步骤 3：配置应用与端口

- 开通后开发者门户中应用状态显示为 **"Ready to Use"**
- 在开发者门户中创建应用时，**回调 URL（Redirect URI）中的端口号建议使用 `8443`**
- 你也可以使用任意端口，但必须满足以下条件：
  - 在开发者门户中注册的回调 URL 中的端口号
  - 必须与 `Quant.Infra.Net.Web` 项目中 `appsettings.json` 的 **`Schwab.OAuth.RedirectUri`** 配置保持一致

![Charles Developer Registry Ready](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Developer%20Registry%20Ready.jpg?raw=true)

### 步骤 4：获取与保存凭据

- 开通后你将获得 **Client ID（App Key）** 和 **Client Secret（App Secret）**
- **Client Secret 仅在创建时显示一次**，请务必立即安全保存
- 凭据应**保密存储**，严禁提交到代码仓库

#### 推荐的安全存储方式

```bash
# 方式一：.NET User Secrets（开发环境）
dotnet user-secrets set "Schwab:ClientId" "你的ClientId"
dotnet user-secrets set "Schwab:ClientSecret" "你的ClientSecret"

# 方式二：环境变量
setx SCHWAB_CLIENT_ID "你的ClientId"
setx SCHWAB_CLIENT_SECRET "你的ClientSecret"
```

#### 配置 `appsettings.json`

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

> **注意**：`ClientId` 和 `ClientSecret` 建议使用 User Secrets 或环境变量配置，不要在 `appsettings.json` 中明文填写。`RedirectUri` 的端口号必须与开发者门户中注册的完全一致。

---

## 配置检查清单

- [ ] 已注册 Charles Schwab Developer 账户
- [ ] 开发者账户状态为 "Ready to Use"
- [ ] 已获取 Client ID 和 Client Secret
- [ ] 开发者门户中的 Redirect URI 端口与应用配置一致
- [ ] 凭据已安全存储（User Secrets / 环境变量）
- [ ] 已更新 `appsettings.json` 中的 `RedirectUri`

---

## 注意事项

- **政策变更**：Charles Schwab 的开发者注册政策可能随时调整（如手机号要求、审核时长等），请以 [developer.schwab.com](https://developer.schwab.com/) 官方信息为准
- **安全问题**：Client Secret 泄露可能导致他人使用你的 API 权限，务必妥善保管
- **合规要求**：API 使用须遵守 Charles Schwab 开发者协议和相关法律法规

---

## 相关文档

- [Charles Schwab OAuth 完整指南](SCHWAB_OAUTH_ULTIMATE_GUIDE.md)
- [Charles Schwab 快速开始](SCHWAB_QUICKSTART.md)
- [Charles Schwab 集成指南](SCHWAB_INTEGRATION_GUIDE.md)

---

**更新日期**: 2026-05-25
**版本**: 1.0.0
**维护者**: Quant.Infra.Net Team
