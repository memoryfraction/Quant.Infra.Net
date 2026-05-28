# Quant.Infra.Net.Pro.Web — Charles Schwab Deployment Guide / Quant.Infra.Net.Pro.Web — Charles Schwab 部署指南

> CharlesSchwab.WebApi can be deployed locally, on a LAN, or on a private VPS. The Web API provides access to account info, US Equity data, and US Option data. For security reasons, trading is not supported in the current version.

**Features: 100% Local, Self-Hosted, Physically Read-Only, Credentials Never Uploaded. "Your Privacy, Your Choice"**

---

- **Applicable Version:** Quant.Infra.Net.Pro.Web
- **Security Statement:** Local / Intranet / Private VPS deployment only. Public internet exposure is strictly prohibited.

> ⚠️ **Prerequisite:** Complete [CharlesSchwabDeveloperRegistryGuide.md](./CharlesSchwabDeveloperRegistryGuide.md) first to obtain your **App Key** and **App Secret** before purchasing a subscription.

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

## 1. System Requirements

- **OS:** Windows / Linux / macOS (x64)
- **Runtime:** .NET 8 Runtime (or SDK)
- **Disk:** 500MB+ free space
- **Network:** Must be able to reach Charles Schwab API endpoints
- **Deployment:** Local / intranet / private VPS only — no public ports

## 2. Pre-Purchase Checklist

1. Register as a Schwab Developer and create a **Private App**
2. Obtain:
   - **App Key** (Client ID)
   - **App Secret**
   - **Redirect URI** — must be set to: `https://127.0.0.1:8443/`
3. Confirm the App status is **Approved**

> ❗ **Do NOT purchase before completing steps 1–3; otherwise the product cannot be configured.**

## 3. Purchase & Installation Package

1. **Purchase a subscription on the official website:**
   - Monthly: **$49.99 / month**
   - Annual: **$499.99 / year** ($41.67 / month — better value)

2. Payment is processed via **Lemon Squeezy** — secure and fraud-free.

3. Within **1 minute** after successful payment, you will receive:
   - Your exclusive **License Key**
   - **Pro Edition** download link
   - This deployment guide

## 4. Updates & Upgrades (Free During Active Subscription)

- All version updates are free while your subscription is active.
- When a new version is released, simply re-click the original download link in your email to get the latest build.

**Update steps:**

1. Stop the current Pro.Web service
2. Extract the new package
3. Overwrite the old files (configuration files are preserved)
4. Restart the service

> Your License Key remains unchanged — no reactivation required.

## 5. Local Deployment

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

### Step 3: Start the Service

**Windows**

```
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

## 6. Connect to Charles Schwab (Authorization)

1. Open your browser and navigate to **http://127.0.0.1:8443/**
2. Click **Connect to Charles Schwab**
3. You will be redirected to the Schwab official login page
4. Enter your Schwab account credentials and authorize
5. You will be automatically redirected back to the local system
6. Account info, positions, and quote data will be displayed

> **Current version is read-only.** Supported features: account balances, positions, quotes, and order history. Trade execution will be added based on customer feedback.

## 7. Feature Previews

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

## 8. LAN Deployment (Multi-Device Access)

1. Deploy on an intranet server or private VPS
2. Ensure the firewall only allows intranet access
3. Access from other intranet devices at: `https://<server-intranet-ip>:8443`
4. **Do NOT** map ports to the public internet
5. **Do NOT** bind to `0.0.0.0`

## 9. License Rules (Enforced)

| Rule | Description |
|------|-------------|
| Device binding | **1 License** bound to **1 device** |
| Concurrency | **Only 1 device online at a time** |
| Expiration | Unrenewed subscription → License auto-deactivates → service stops |
| Device swap | Contact support to unbind the old device |

## 10. Security Deployment Standards (Mandatory)

| Status | Standard |
|--------|----------|
| ✅ | Local / intranet / private VPS only |
| ✅ | Credentials stored on your machine only |
| ✅ | Data never uploaded to third-party servers |
| ❌ | No public internet access |
| ❌ | No domain name exposed to the public |
| ❌ | Do not share License Key with others |

## 11. FAQ

### 1. Callback Failure

- Verify `RedirectUri` matches exactly
- Confirm the Schwab App is **Approved**

### 2. License Activation Failure

- Check network connectivity
- Confirm `LicenseKey` is entered correctly

### 3. Port Conflict

- Change the port in `Urls`
- Update `RedirectUri` in the Schwab developer portal accordingly

### 4. How to Update

- Re-click the download link in your email → overwrite old files → restart

---

> **This project is in preparation. Purchase links will be available when the official release launches. Stay tuned.**

---

# 中文版

## 适用场景

| 版本 | 适用人群 |
|------|---------|
| **社区版**（开源） | 适合学习量化、本地回测，有一定动手能力，且有时间折腾的开发者 |
| **Pro 版**（本产品） | 适合希望开箱即用，没有时间折腾的量化从业者 |

## 一、系统环境要求

- **操作系统：** Windows / Linux / macOS（x64）
- **运行时：** .NET 8 运行时（或 SDK）
- **磁盘空间：** 500MB 以上空闲磁盘
- **网络：** 可访问 Charles Schwab API
- **部署限制：** 仅内网 / 本地运行，不开放公网端口

## 二、购买前必须完成

1. 注册 Schwab Developer 并创建 Private App
2. 获取：
   - **App Key**（Client ID）
   - **App Secret**
   - **Redirect URI** 必须设置为：https://127.0.0.1:8443/
3. 确认 App 状态为 **Approved**

> ❗ **未完成以上步骤请勿购买，否则无法配置使用。**

## 三、购买与获取安装包

1. **前往官网购买订阅：**
   - 月度订阅：**$49.99 / 月**
   - 年度订阅：**$499.99 / 年**（折合 $41.67 / 月，更划算）
2. 支付通过 **Lemon Squeezy** 完成，安全无盗刷。
3. 支付成功后 **1 分钟内**，您将收到以下信息：
   - 专属 **License Key**
   - **Pro 版本安装包** 下载链接
   - 本部署文档

## 四、版本更新说明（订阅期内免费更新）

- 只要订阅在有效期内，所有版本更新免费。
- 新版本发布后，直接重新点击邮件里的原下载链接即可获取最新版。

**更新步骤：**

1. 停止当前 Pro.Web 服务
2. 解压新版文件
3. 直接覆盖旧版本（配置文件不会丢失）
4. 重新启动即可

> License Key 不变，无需重新激活。

## 五、本地部署步骤

### 步骤 1：解压程序

将下载的 ZIP 包解压到本地目录，例如：
- **Windows：** C:\QuantInfra\Pro.Web
- **Linux：** /home/quant/Pro.Web

### 步骤 2：配置 appsettings.json

打开 appsettings.json，填写以下 3 部分信息：

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

### 步骤 3：启动服务

**Windows**

```
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

## 六、连接 Charles Schwab 授权

1. 打开浏览器访问：**http://127.0.0.1:8443/**
2. 点击 **Connect to Charles Schwab** 跳转到 Schwab 官方登录页
3. 输入 Schwab 投资账号并完成授权
4. 自动跳转回本地系统，显示账户信息、持仓、行情数据

> 当前版本为 **只读模式**，支持：账户资产、持仓、行情、历史数据查询，暂不支持下单交易。根据客户反馈，可能增加下单功能。

## 七、功能界面预览

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

## 八、局域网部署（多设备访问）

1. 将程序部署在内网服务器 / VPS
2. 确保防火墙仅开放内网访问
3. 其他内网设备访问：https://服务器内网IP:8443
4. **禁止** 端口映射到公网
5. **禁止** 使用 0.0.0.0 监听

## 九、授权规则（强制执行）

| 规则 | 说明 |
|------|------|
| 设备绑定 | **1 个 License** 绑定 **1 台设备** |
| 并发限制 | 同一时间 **仅允许 1 台设备在线** |
| 续费机制 | 订阅到期未续费 → License 自动失效 → 功能停止 |
| 设备更换 | 需联系支持解绑旧设备 |

## 十、安全部署规范（强制）

| 状态 | 规范 |
|------|------|
| ✅ | 仅本地 / 内网 / 私有 VPS 部署 |
| ✅ | 凭证仅保存在你自己的机器 |
| ✅ | 数据不上传到第三方服务器 |
| ❌ | 不开放公网访问 |
| ❌ | 不使用域名对外提供服务 |
| ❌ | 不分享 License 给他人 |

## 十一、常见问题

### 1. 回调失败

- 检查 RedirectUri 是否完全一致
- 确认 Schwab App 已 **Approved**

### 2. License 激活失败

- 检查网络连通性
- 确认 LicenseKey 输入正确

### 3. 端口被占用

- 修改 Urls 中的端口
- 同步更新 Schwab 后台 RedirectUri

### 4. 如何更新版本

- 重新点击邮件下载链接 → 覆盖旧版 → 重启即可

---

> **项目筹备中，正式开放时会放上付费链接，敬请期待。**
