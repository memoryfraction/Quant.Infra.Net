# Quant.Infra.Net.Pro.Web — Charles Schwab Deployment Guide / Quant.Infra.Net.Pro.Web — Charles Schwab 部署指南

> CharlesSchwab.WebApi can be deployed locally, on a LAN, or on a private VPS. The Web API provides access to account info, US Equity data, and US Option data. For security reasons, trading is not supported in the current version.
> CharlesSchwab.WebApi 支持本地、局域网或私有 VPS 部署，提供账户资产、美股行情、美股期权数据访问。当前版本为只读模式，暂不支持下单交易。

**Features: 100% Local, Self-Hosted, Physically Read-Only, Credentials Never Uploaded. "Your Privacy, Your Choice"**

- **Applicable Version:** Quant.Infra.Net.Pro.Web
- **Security Statement:** Local / Intranet / Private VPS deployment only. Public internet exposure is strictly prohibited.

---

## Version History / 版本历史

| Version / 版本 | Date / 日期 | Description / 描述 |
|------|------|-------------|
| 1.0.0 | 2026-05-28 | Initial release: deployment guide for Quant.Infra.Net.Pro.Web / Quant.Infra.Net.Pro.Web 部署指南初版发布 |

---

# English

## Use Cases

| Edition | Suitable For |
|---------|-------------|
| **Community Edition** (Open Source) | Developers who want to learn quantitative trading, run local backtests, have hands-on DIY skills, and have time to tinker with configurations |
| **Pro Edition** (This Product) | Quantitative practitioners who want an out-of-the-box experience and have no time for tinkering |

---

## Prerequisite: Complete Developer Registration

> **You must complete the developer registration BEFORE purchasing. Otherwise the product cannot be configured.**

Follow the **[Charles Schwab Developer Registration Guide](CharlesSchwabDeveloperRegistryGuide.md)** to:

1. Register a Charles Schwab Developer account
2. Create an application
3. Obtain your **App Key (Client ID)** and **App Secret (Client Secret)**
4. Configure the **Redirect URI** in the developer portal (e.g., `https://127.0.0.1:8443/`)
5. Ensure the application status is **Approved**

After completing these steps, proceed with the purchase below.

---

## 1. System Requirements

- **OS:** Windows / Linux / macOS (x64)
- **Runtime:** .NET 8 Runtime (or SDK)
- **Disk:** 500MB+ free space
- **Network:** Must be able to reach Charles Schwab API endpoints
- **Deployment:** Local / intranet / private VPS only — no public ports

---

## 2. Purchase & Installation Package

1. **Purchase a subscription on the official website:**
   - Monthly: **$49.99 / month**
   - Annual: **$499.99 / year** ($41.67 / month — better value)
2. Payment is processed via **Lemon Squeezy** — secure and fraud-free.
3. Within **1 minute** after successful payment, you will receive:
   - Your exclusive **License Key**
   - **Pro Edition** download link (ZIP package)
   - This deployment guide

### Updates & Upgrades (Free During Active Subscription)

- All version updates are free while your subscription is active.
- When a new version is released, simply re-click the original download link in your email to get the latest build.

**Update steps:**
1. Stop the current Pro.Web service
2. Extract the new package
3. Overwrite the old files (configuration files are preserved)
4. Restart the service

> Your License Key remains unchanged — no reactivation required.

---

## 3. Local Deployment

### Step 1: Extract the Package

Unzip the downloaded package to a local directory, for example:
- **Windows:** `C:\QuantInfra\Pro.Web`
- **Linux:** `/home/quant/Pro.Web`

### Step 2: Configure appsettings.json

Edit `appsettings.json` and fill in the following 3 sections:

```json
{
  "Schwab": {
    "AppKey": "Your Schwab App Key",
    "AppSecret": "Your Schwab App Secret",
    "RedirectUri": "https://127.0.0.1:8443/"
  },
  "License": {
    "LicenseKey": "Your License Key",
    "LicenseServerUrl": "https://api.quant.infra.net"
  },
  "Urls": "https://127.0.0.1:8443;http://127.0.0.1:8443"
}
```

**Configuration Details:**

| Setting | Description |
|---------|-------------|
| `Schwab.AppKey` | Your Client ID from the developer portal |
| `Schwab.AppSecret` | Your Client Secret from the developer portal |
| `Schwab.RedirectUri` | Must exactly match the callback URL in the developer portal |
| `License.LicenseKey` | The License Key received after purchase |
| `License.LicenseServerUrl` | License validation server (do not change) |
| `Urls` | Binding addresses for the web server |

### Step 3: Start the Service

**Windows**

```bash
Quant.Infra.Net.Pro.Web.exe
```

**Linux / macOS**

```bash
dotnet Quant.Infra.Net.Pro.Web.dll
```

**Successful startup output:**

```
Now listening on: https://127.0.0.1:8443
Now listening on: http://127.0.0.1:8443
Application started.
```

---

## 4. Connect to Charles Schwab (OAuth Authorization)

1. Open your browser and navigate to **http://127.0.0.1:8443/**
2. Click **Connect to Charles Schwab**
3. You will be redirected to the Schwab official login page
4. Enter your Schwab trading account credentials and authorize
5. You will be automatically redirected back to the local system
6. Account info, positions, and quote data will be displayed

> **Current version is read-only.** Supported features: account balances, positions, quotes, and order history. Trade execution will be added based on customer feedback.

### Login Troubleshooting

**Authorization code exchange fails:**
- Verify `Client ID` and `Client Secret` are correct
- Verify the `RedirectUri` in appsettings.json matches exactly what's registered in the developer portal
- Schwab's authorization code can only be used once. If it fails, restart the OAuth process from the homepage
- Ensure you complete the entire OAuth flow in the same browser session

**Account or positions not showing:**
- Make sure you entered the **Schwab trading account number**, not your login username
- Confirm the developer application has Trader API permissions
- Ensure you authorized account access during the OAuth flow

---

## 5. Feature Previews

### Account Overview

![Account](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Account.jpg?raw=true)

### Positions

![Positions](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Positions.jpg?raw=true)

### Quote

![Quote](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Quote.jpg?raw=true)

### Option Chain

![Option Chain](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Option%20Chain.jpg?raw=true)

### Order History

![Order History](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Order%20History.jpg?raw=true)

### Web API Swagger

![WebApi Swagger](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/WebApi%20Swagger.jpg?raw=true)

---

## 6. LAN Deployment (Multi-Device Access)

1. Deploy on an intranet server or private VPS
2. Ensure the firewall only allows intranet access
3. Access from other intranet devices at: `https://<server-intranet-ip>:8443`
4. **Do NOT** map ports to the public internet
5. **Do NOT** bind to `0.0.0.0`

---

## 7. License Rules (Enforced)

| Rule | Description |
|------|-------------|
| Device binding | **1 License** bound to **1 device** |
| Concurrency | **Only 1 device online at a time** |
| Expiration | Unrenewed subscription -> License auto-deactivates -> service stops |
| Device swap | Contact support to unbind the old device |

---

## 8. Security Deployment Standards (Mandatory)

| Status | Standard |
|--------|----------|
| OK | Local / intranet / private VPS only |
| OK | Credentials stored on your machine only |
| OK | Data never uploaded to third-party servers |
| NOT OK | No public internet access |
| NOT OK | No domain name exposed to the public |
| NOT OK | Do not share License Key with others |

---

## 9. FAQ

### 1. Callback Failure

- Verify `RedirectUri` matches exactly between the developer portal and appsettings.json
- Confirm the Schwab App is **Approved**
- Make sure the port is not occupied by another application

### 2. License Activation Failure

- Check network connectivity to the license server
- Confirm `LicenseKey` is entered correctly
- Contact support if the issue persists

### 3. Port Conflict

- Change the port in the `Urls` setting
- Update `RedirectUri` in the Schwab developer portal accordingly
- Restart the service

### 4. How to Update

- Re-click the download link from your purchase email
- Overwrite old files (config is preserved)
- Restart the service

### 5. Can I trade with this version?

- The current version is **read-only**. Trading support may be added in future versions based on customer feedback.

### 6. Can I deploy on a public cloud?

- **No.** This product is restricted to local, intranet, or private VPS deployment only. Public internet exposure is strictly prohibited.

---

> **This project is in preparation. Purchase links will be available when the official release launches. Stay tuned.**

---

# 中文版

## 适用场景

| 版本 | 适用人群 |
|------|---------|
| **社区版**（开源） | 适合学习量化、本地回测，有一定动手能力且有时间折腾的开发者 |
| **Pro 版**（本产品） | 适合希望开箱即用，没有时间折腾的量化从业者 |

---

## 前置条件：先完成开发者注册

> **必须先完成开发者注册再购买，否则产品无法配置使用。**

请先阅读 **[Charles Schwab Developer 注册指南](CharlesSchwabDeveloperRegistryGuide.md)** 完成以下步骤：

1. 注册 Charles Schwab Developer 账户
2. 创建应用
3. 获取 **App Key（Client ID）** 和 **App Secret（Client Secret）**
4. 在开发者门户配置 **Redirect URI**（例如 `https://127.0.0.1:8443/`）
5. 确认应用状态为 **Approved**

完成以上步骤后，再继续下面的购买流程。

---

## 一、系统环境要求

- **操作系统：** Windows / Linux / macOS（x64）
- **运行时：** .NET 8 运行时（或 SDK）
- **磁盘空间：** 500MB 以上空闲磁盘
- **网络：** 可访问 Charles Schwab API
- **部署限制：** 仅本地 / 内网 / 私有 VPS 运行，不开放公网端口

---

## 二、购买与获取安装包

1. **前往官网购买订阅：**
   - 月度订阅：**$49.99 / 月**
   - 年度订阅：**$499.99 / 年**（折合 $41.67 / 月，更划算）
2. 支付通过 **Lemon Squeezy** 完成，安全无盗刷
3. 支付成功后 **1 分钟内**，您将收到以下信息：
   - 专属 **License Key**
   - **Pro 版本安装包** 下载链接（ZIP 包）
   - 本部署文档

### 版本更新说明（订阅期内免费更新）

- 只要订阅在有效期内，所有版本更新免费
- 新版本发布后，直接重新点击邮件里的原下载链接即可获取最新版

**更新步骤：**
1. 停止当前 Pro.Web 服务
2. 解压新版文件
3. 直接覆盖旧版本（配置文件不会丢失）
4. 重新启动即可

> License Key 不变，无需重新激活

---

## 三、本地部署步骤

### 步骤 1：解压程序

将下载的 ZIP 包解压到本地目录，例如：
- **Windows：** `C:\QuantInfra\Pro.Web`
- **Linux：** `/home/quant/Pro.Web`

### 步骤 2：配置 appsettings.json

打开 `appsettings.json`，填写以下 3 部分信息：

```json
{
  "Schwab": {
    "AppKey": "你的 Schwab App Key",
    "AppSecret": "你的 Schwab App Secret",
    "RedirectUri": "https://127.0.0.1:8443/"
  },
  "License": {
    "LicenseKey": "你收到的 License Key",
    "LicenseServerUrl": "https://api.quant.infra.net"
  },
  "Urls": "https://127.0.0.1:8443;http://127.0.0.1:8443"
}
```

**配置项说明：**

| 配置项 | 说明 |
|--------|------|
| `Schwab.AppKey` | 开发者门户获取的 Client ID |
| `Schwab.AppSecret` | 开发者门户获取的 Client Secret |
| `Schwab.RedirectUri` | 必须与开发者门户注册的回调 URL 完全一致 |
| `License.LicenseKey` | 购买后收到的 License Key |
| `License.LicenseServerUrl` | 授权验证服务器地址（请勿修改） |
| `Urls` | Web 服务绑定地址 |

### 步骤 3：启动服务

**Windows**

```bash
Quant.Infra.Net.Pro.Web.exe
```

**Linux / macOS**

```bash
dotnet Quant.Infra.Net.Pro.Web.dll
```

**启动成功显示：**

```
Now listening on: https://127.0.0.1:8443
Now listening on: http://127.0.0.1:8443
Application started.
```

---

## 四、连接 Charles Schwab 授权

1. 打开浏览器访问：**http://127.0.0.1:8443/**
2. 点击 **Connect to Charles Schwab** 跳转到 Schwab 官方登录页
3. 输入 Schwab 投资账号凭据并完成授权
4. 自动跳转回本地系统，显示账户信息、持仓、行情数据

> 当前版本为 **只读模式**，支持：账户资产、持仓、行情、历史数据查询，暂不支持下单交易。

### 登录常见问题

**授权码换 Token 失败：**
- 检查 Client ID 和 Client Secret 是否正确
- 确认 appsettings.json 中的 `RedirectUri` 与开发者门户注册的完全一致
- Schwab 的授权码只能使用一次，失败后请重新从首页开始授权流程
- 确保在同一个浏览器会话中完成整个 OAuth 流程

**看不到账户或持仓：**
- 填写的是 **Schwab 交易账户号**，不是登录用户名
- 确认开发者应用已开通 Trader API 权限
- 确认授权时已允许访问账户

---

## 五、功能界面预览

### 账户总览

![Account](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Account.jpg?raw=true)

### 持仓信息

![Positions](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Positions.jpg?raw=true)

### 行情查询

![Quote](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Quote.jpg?raw=true)

### 期权链

![Option Chain](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Option%20Chain.jpg?raw=true)

### 历史订单

![Order History](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/Order%20History.jpg?raw=true)

### Web API Swagger

![WebApi Swagger](https://github.com/memoryfraction/Quant.Infra.Net/blob/main/images/Charles%20Schwab/WebApi%20Swagger.jpg?raw=true)

---

## 六、局域网部署（多设备访问）

1. 将程序部署在内网服务器 / 私有 VPS
2. 确保防火墙仅开放内网访问
3. 其他内网设备访问：`https://服务器内网IP:8443`
4. **禁止**端口映射到公网
5. **禁止**使用 `0.0.0.0` 监听

---

## 七、授权规则（强制执行）

| 规则 | 说明 |
|------|------|
| 设备绑定 | **1 个 License** 绑定 **1 台设备** |
| 并发限制 | 同一时间**仅允许 1 台设备在线** |
| 续费机制 | 订阅到期未续费 -> License 自动失效 -> 功能停止 |
| 设备更换 | 需联系支持解绑旧设备 |

---

## 八、安全部署规范（强制）

| 状态 | 规范 |
|------|------|
| 通过 | 仅本地 / 内网 / 私有 VPS 部署 |
| 通过 | 凭证仅保存在你自己的机器上 |
| 通过 | 数据不上传到第三方服务器 |
| 禁止 | 不开放公网访问 |
| 禁止 | 不使用域名对外提供服务 |
| 禁止 | 不分享 License 给他人 |

---

## 九、常见问题

### 1. 回调失败

- 检查 `RedirectUri` 在开发者门户和 appsettings.json 中是否完全一致
- 确认 Schwab App 状态为 **Approved**
- 确认端口未被其他程序占用

### 2. License 激活失败

- 检查网络连通性
- 确认 `LicenseKey` 输入正确
- 如问题持续，请联系技术支持

### 3. 端口被占用

- 修改 `Urls` 中的端口号
- 同步更新 Schwab 开发者门户中的 `RedirectUri`
- 重启服务

### 4. 如何更新版本

- 重新点击购买邮件中的下载链接
- 解压覆盖旧版（配置文件会保留）
- 重启服务即可

### 5. 当前版本可以交易吗？

- 当前版本为**只读模式**，暂不支持下单交易。下单功能可能根据用户反馈在后续版本加入。

### 6. 可以部署在公网云服务器吗？

- **不可以。** 本产品仅限本地、内网或私有 VPS 部署，严禁暴露到公网。

---

> **项目筹备中，正式开放时会放上付费链接，敬请期待。**

---

> **Disclaimer**: See [DISCLAIMER.md](../DISCLAIMER.md) for full disclaimer and limitation of liability / 详见 [免责声明](../DISCLAIMER.md) 了解完整免责条款与责任限制。

---

**更新日期**: 2026-05-28
**版本**: 1.0.0
**维护者**: Quant.Infra.Net Team
