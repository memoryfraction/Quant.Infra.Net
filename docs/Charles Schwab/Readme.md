# Quant.Infra.Net.Pro.CharlesSchwab.Web

[![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/8.0)

> **Quant.Infra.Net.Pro** 是基于 .NET 8 的 Charles Schwab 一站式对接方案，提供 OAuth 认证、实时行情、账户管理、订单执行、期权链分析。

> 官网：**[https://www.alpha-wealth-lab.com/products/quantinfrapro](https://www.alpha-wealth-lab.com/products/quantinfrapro)**

---

## Version History

| Version | Date | Description |
|---------|------|-------------|
| 1.0.0 | 2025-05-29 | Initial release: Charles Schwab OAuth login, account management, quotes, order execution, LicenseForge telemetry-based license validation, EULA acceptance, Swagger API docs, HTTPS self-signed certificate, single-file deployment |
| 1.1.0 | 2025-05-29 | Product name updated to `quant.infra.net.pro`; added explicit `License:ProductCode` configuration in appsettings.json |
| 1.1.1 | 2026-05-30 | Improved version badge visibility on dark backgrounds; added version badge to license expired page |
| 1.1.2 | 2026-05-30 | Activate page now provides an input form to enter and save license key directly — no manual appsettings.json editing required |
| 1.2.0 | 2026-05-30 | Architecture refactoring: extracted `AccountManager` and `SchwabAuthManager` from Controllers (thin-layer pattern), renamed `SwaggerConfig` → `SwaggerExtensions`, unified API route prefix to `api/v1/`, removed `appsettings.Development.json` |

---

# English

## Scenario

You are a **quantitative practitioner or individual investor** who uses **Charles Schwab** as your primary US stock/options brokerage. You want to:

- Monitor your Schwab account balances, positions, and P/L in real-time
- Look up stock quotes and full option chains (with Greeks) without switching between multiple platforms
- Execute trades (market orders, limit orders) programmatically or via a web UI
- Build quantitative strategies (covered calls, cash-secured puts, pairs trading, delta hedging, etc.) that require reliable, structured access to Schwab account and market data

In short: You want secondary development, without handling Charles Schwab integration.

---

## Pain Points

| Pain Point | Impact |
|------------|--------|
| **OAuth 2.0 authentication is complex** — Schwab requires exact Redirect URI matching, HTTPS callbacks, and a multi-step token exchange | Developers spend days on auth plumbing before writing a single line of business logic |
| **Developer registration barrier is high** — you must register at developer.schwab.com, create an app, wait 1-3 business days for approval, and manage Client ID / Client Secret securely | Non-developers or busy traders cannot get started |

---

## How Quant.Infra.Net.Pro.CharlesSchwab.Web Solves These Problems

| Problem | Solution |
|---------|----------|
| Complex OAuth 2.0 authentication | **Pre-configured OAuth flow** with HTTPS self-signed certificate on port 8443 — no admin rights needed; automatic token refresh; session management |
| High developer registration barrier | **Full registration guide** ([Developer Registry Guide](CharlesSchwabDeveloperRegistryGuide-01.md)) walks you through every step, with screenshots |
| Data scattered across APIs | **Unified Web UI Dashboard** — account balances, positions, P/L, quotes, option chains, order history in one page; no coding required |
| Credential security concerns | **100% self-hosted** — credentials stay on your machine; OAuth tokens stored locally; data never uploaded to any third-party server |
| Option data complexity | **Full option chain with Greeks** — Delta, Gamma, Theta, Vega, Rho, Implied Volatility, Open Interest, all normalized and displayed |
| High development cost | **Zero configuration** — download, fill in 3 settings (App Key, App Secret, License Key), run. Total setup time: under 5 minutes |
| Need programmatic access | **Full RESTful Web API with Swagger** — every feature accessible via `api/v1/` endpoints for integration into your own trading bots or scripts |

---

## What Problems It Solves — Quantitative Strategy Use Cases

With `Quant.Infra.Net.Pro.CharlesSchwab.Web`, you can implement the following investment strategies:

| Strategy | How Pro Edition Helps |
|----------|----------------------|
| **Covered Call Writing** | View your stock positions + option chain with Greeks → identify high-premium OTM calls → place orders via API |
| **Cash-Secured Put Selling** | Check cash balance + put option chain → find puts with attractive premiums and appropriate strike prices |
| **Pairs Trading (US Equities)** | Use Community Edition's `IAnalysisService` for ADF/OLS/Z-Score analysis, then use Pro Edition to monitor positions and execute orders on Schwab |
| **Delta Hedging** | Real-time Greeks display (Delta, Gamma, Theta) enables continuous delta monitoring and timely hedge adjustments |
| **Wheel Strategy** | Track assigned shares, sell covered calls, monitor put assignments — all from one dashboard |
| **Portfolio Rebalancing** | Real-time P/L calculation across all positions → identify overweight/underweight positions → execute rebalancing trades |
| **Volatility Trading** | Monitor implied volatility across option chains → compare IV vs HV → identify overpriced/underpriced options |
| **Dividend Capture** | Account summary + positions view → track ex-dividend dates → plan entry/exit around dividend payments |

---

## Quantitative Practice: From Data to Execution

| Stage | Community Edition (Free) | Pro Edition ([Pricing](https://www.alpha-wealth-lab.com/pricing)) |
|-------|--------------------------|-------------------------|
| **Data Acquisition** | Yahoo Finance, Binance, CSV/MySQL/MongoDB | + Schwab real-time quotes, option chains |
| **Statistical Analysis** | ADF, OLS, Z-Score, correlation, Shapiro-Wilk | (same) |
| **Strategy Development** | Python/C# backtesting via pythonnet | (same) |
| **Broker Integration** | Binance, Alpaca, Schwab (code), Interactive Brokers | Schwab (Web UI + API, zero code) |
| **Portfolio Monitoring** | `StrategyPerformanceAnalyzer` (CAGR/Sharpe/Calmar/MDD) | + Live Schwab account dashboard with real-time P/L |
| **Trade Execution** | `ISchwabBrokerService` (C# API calls) | Web UI one-click + RESTful API |
| **Notifications** | DingTalk, WeChat Work, Email | (same) |

---

## Pricing

See [Pricing Page](https://www.alpha-wealth-lab.com/pricing) for the latest pricing and plan details.

> **All Community Edition features remain free and will continue to be maintained.** The Pro Edition is a separate product for those who need a ready-to-use Schwab web dashboard.

**Purchase:** [https://www.alpha-wealth-lab.com/products/quantinfrapro](https://www.alpha-wealth-lab.com/products/quantinfrapro)

---

## Quick Start

### Prerequisites

1. A **Charles Schwab trading account** — [schwab.com](https://www.schwab.com/)
2. A **Charles Schwab Developer account** — follow the [Developer Registry Guide](CharlesSchwabDeveloperRegistryGuide-01.md)
3. A **Pro Edition License Key** — purchase from [official website](https://www.alpha-wealth-lab.com/products/quantinfrapro)

### Step 1: Download & Extract

Unzip the downloaded Pro package to a local directory, e.g.:
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

### Step 4: Connect to Charles Schwab

1. Open browser → **http://127.0.0.1:8443/**
2. Click **Connect to Charles Schwab**
3. Log in with your Schwab trading account credentials
4. Authorize the application
5. Done — account info, positions, and quotes are displayed

---

## Feature Previews

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

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    User Browser                              │
│                  https://127.0.0.1:8443                      │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│              Quant.Infra.Net.Pro.Web (.NET 8)                │
│  ┌──────────────────────────────────────────────────────┐   │
│  │          LicenseValidationMiddleware                  │   │
│  │  1. Key empty? → Redirect /activate (or API 401)     │   │
│  │  2. Status=Valid → Pass through + telemetry          │   │
│  │  3. Status=Unknown/NetworkError → Pass + background  │   │
│  │     validation (non-blocking)                        │   │
│  │  4. Status=Expired/Revoked → Block + error page      │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────┐  ┌────────────────────────────┐   │
│  │  LicenseCacheStore  │  │  LicenseTelemetryService   │   │
│  │  (Singleton)        │  │  (Singleton)               │   │
│  └─────────────────────┘  └────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────┐  ┌────────────────────────────┐   │
│  │  AccountManager     │  │  SchwabAuthManager         │   │
│  │  (Scoped)           │  │  (Scoped)                  │   │
│  └─────────────────────┘  └────────────────────────────┘   │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Controllers & Razor Pages                │   │
│  │  - /api/v1/license/status  (License diagnostics)     │   │
│  │  - /api/v1/license/refresh (Force re-validation)     │   │
│  │  - /api/v1/accounts        (Account management)      │   │
│  │  - /api/v1/quotes          (Market quotes)           │   │
│  │  - /api/v1/orders          (Order management)        │   │
│  │  - /Dashboard              (Main UI)                 │   │
│  │  - /activate               (License activation)      │   │
│  │  - /license-expired        (Error page)              │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                       │ HTTP POST
┌──────────────────────▼──────────────────────────────────────┐
│           LicenseForge (Azure Container Apps)                │
│  POST /api/v1/licenses/validate                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Security Deployment Standards

| Status | Standard |
|--------|----------|
| OK | Local / intranet / private VPS only |
| OK | Credentials stored on your machine only |
| OK | Data never uploaded to third-party servers |
| NOT OK | No public internet access |
| NOT OK | No domain name exposed to the public |
| NOT OK | Do not share License Key with others |

---

## Related Documents

| Document | Description |
|----------|-------------|
| [Developer Registry Guide](CharlesSchwabDeveloperRegistryGuide-01.md) | Step-by-step guide to register a Charles Schwab Developer account and obtain API credentials |
| [Deployment Guide](CharlesSchwabDeploymentGuide-02.md) | Detailed deployment instructions for Pro Edition |
| [Feature Summary](SCHWAB_FEATURE_SUMMARY.md) | Complete feature list for the Community Edition Charles Schwab integration |

---

## Community & Support

- Telegram: [https://t.me/+VPy-VLis8gVmYWM1](https://t.me/+VPy-VLis8gVmYWM1)
- Commercial licensing & custom development: rex.fan18@gmail.com

---

> **Disclaimer**: See [DISCLAIMER.md](../DISCLAIMER.md) for full disclaimer and limitation of liability.

---

# 中文版

## 场景

你是一位**量化从业者或个人投资者**，使用 **Charles Schwab** 作为主要的美股/期权券商。你希望：

- 实时监控 Schwab 账户余额、持仓、盈亏
- 一键查询股票报价和完整期权链（含希腊字母），无需在多个平台之间切换
- 通过 Web 界面或 API 编程式地下单（市价单、限价单）
- 构建量化策略（备兑看涨、卖出看跌、配对交易、Delta 对冲等），这些策略需要可靠、结构化的 Schwab 账户和行情数据

简而言之：你希望**一个统一的仪表盘**查看一切并执行操作——而不需要自己搭建整个对接方案。

---

## 痛点

| 痛点 | 影响 |
|------|------|
| **OAuth 2.0 认证流程复杂** — Schwab 要求 Redirect URI 精确匹配、HTTPS 回调、多步 Token 交换 | 开发者在写业务逻辑之前，就要花数天时间处理认证管道 |
| **开发者注册门槛高** — 必须在 developer.schwab.com 注册、创建应用、等待 1-3 个工作日审核、安全管理 Client ID / Client Secret | 非开发者或忙碌的交易者无法入门 
---

## Quant.Infra.Net.Pro.CharlesSchwab.Web 如何解决问题

| 痛点 | 解决方案 |
|------|---------|
| 复杂的 OAuth 2.0 认证 | **预配置 OAuth 流程** + HTTPS 自签名证书，端口 8443 无需管理员权限；自动 Token 刷新；会话管理 |
| 开发者注册门槛高 | **完整注册指南**（[开发者注册指南](CharlesSchwabDeveloperRegistryGuide-01.md)）逐步引导，附截图 |
| 数据分散在不同 API | **统一 Web UI 仪表盘** — 账户余额、持仓、盈亏、报价、期权链、历史订单一页查看，无需编程 |
| 凭证安全顾虑 | **100% 本地自托管** — 凭证仅保存在你的机器上；OAuth Token 本地存储；数据不上传任何第三方 |
| 期权数据复杂 | **完整期权链含希腊字母** — Delta、Gamma、Theta、Vega、Rho、隐含波动率、未平仓合约，全部标准化展示 |
| 开发成本高 | **零配置** — 下载、填写 3 个参数（App Key、App Secret、License Key）、运行。总设置时间：不到 5 分钟 |
| 需要编程式访问 | **完整 RESTful Web API + Swagger** — 所有功能均可通过 `api/v1/` 端点访问，方便集成到你自己的交易机器人或脚本 |

---

## 能解决什么问题 — 量化策略实战场景

使用 `Quant.Infra.Net.Pro.CharlesSchwab.Web`，你可以实现以下投资策略：

| 策略 | Pro 版如何助力 |
|------|---------------|
| **备兑看涨期权（Covered Call）** | 查看股票持仓 + 期权链含 Greeks → 识别高权利金的虚值看涨期权 → 通过 API 下单 |
| **卖出看跌期权（Cash-Secured Put）** | 检查现金余额 + 看跌期权链 → 找到权利金合适、行权价适当的看跌期权 |
| **美股配对交易（Pairs Trading）** | 使用 Community Edition 的 `IAnalysisService` 做 ADF/OLS/Z-Score 分析，再用 Pro Edition 在 Schwab 上监控持仓并执行交易 |
| **Delta 对冲** | 实时 Greeks 展示（Delta、Gamma、Theta）→ 持续监控 Delta → 及时调整对冲头寸 |
| **轮转策略（Wheel Strategy）** | 跟踪被指派的股票、卖出备兑看涨期权、监控看跌期权指派 — 全部在一个仪表盘完成 |
| **投资组合再平衡** | 实时计算所有持仓的盈亏 → 识别超配/低配头寸 → 执行再平衡交易 |
| **波动率交易** | 监控期权链的隐含波动率 → 对比 IV vs HV → 识别高估/低估期权 |
| **股息捕获（Dividend Capture）** | 账户摘要 + 持仓视图 → 跟踪除息日 → 围绕股息安排进出场 |

---

## 量化实践：从数据到执行

| 阶段 | Community Edition（免费） | Pro Edition（[定价](https://www.alpha-wealth-lab.com/pricing)） |
|------|--------------------------|-------------------------|
| **数据获取** | Yahoo Finance、Binance、CSV/MySQL/MongoDB | + Schwab 实时报价、期权链 |
| **统计分析** | ADF、OLS、Z-Score、相关性、Shapiro-Wilk | （同上） |
| **策略开发** | Python/C# 回测（via pythonnet） | （同上） |
| **券商对接** | Binance、Alpaca、Schwab（代码）、Interactive Brokers | Schwab（Web UI + API，零代码） |
| **组合监控** | `StrategyPerformanceAnalyzer`（CAGR/Sharpe/Calmar/MDD） | + Schwab 实时账户仪表盘含实时盈亏 |
| **交易执行** | `ISchwabBrokerService`（C# API 调用） | Web UI 一键下单 + RESTful API |
| **通知推送** | 钉钉、企业微信、邮件 | （同上） |

---

## 定价

详见[定价页面](https://www.alpha-wealth-lab.com/pricing)了解最新价格和方案详情。

> **所有 Community Edition 功能保持免费，会继续维护和更新。** Pro Edition 是独立产品，适合需要开箱即用 Schwab Web 仪表盘的用户。

**购买：** [https://www.alpha-wealth-lab.com/products/quantinfrapro](https://www.alpha-wealth-lab.com/products/quantinfrapro)

---

## 快速开始

### 前置条件

1. **Charles Schwab 交易账户** — [schwab.com](https://www.schwab.com/)
2. **Charles Schwab Developer 账户** — 按照[开发者注册指南](CharlesSchwabDeveloperRegistryGuide-01.md)操作
3. **Pro Edition License Key** — 从[官网](https://www.alpha-wealth-lab.com/products/quantinfrapro)购买

### 第一步：下载并解压

将下载的 Pro 安装包解压到本地目录，例如：
- **Windows：** `C:\QuantInfra\Pro.Web`
- **Linux：** `/home/quant/Pro.Web`

### 第二步：配置 appsettings.json

编辑 `appsettings.json`，填写以下 3 部分信息：

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

### 第三步：启动服务

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

### 第四步：连接 Charles Schwab

1. 打开浏览器访问 **http://127.0.0.1:8443/**
2. 点击 **Connect to Charles Schwab** 跳转到 Schwab 官方登录页
3. 输入 Schwab 投资账号凭据并完成授权
4. 完成 — 账户信息、持仓、行情数据一览无余

---

## 功能界面预览

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

## 架构

```
┌─────────────────────────────────────────────────────────────┐
│                    用户浏览器                                 │
│                  https://127.0.0.1:8443                      │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│              Quant.Infra.Net.Pro.Web (.NET 8)                │
│  ┌──────────────────────────────────────────────────────┐   │
│  │          LicenseValidationMiddleware                  │   │
│  │  1. Key 为空？→ 重定向 /activate（或 API 401）         │   │
│  │  2. Status=Valid → 放行 + 遥测信号                     │   │
│  │  3. Status=Unknown/NetworkError → 放行 + 后台验证      │   │
│  │  4. Status=Expired/Revoked → 阻断 + 错误页面           │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────┐  ┌────────────────────────────┐   │
│  │  LicenseCacheStore  │  │  LicenseTelemetryService   │   │
│  │  (单例)              │  │  (单例)                     │   │
│  └─────────────────────┘  └────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────┐  ┌────────────────────────────┐   │
│  │  AccountManager     │  │  SchwabAuthManager         │   │
│  │  (作用域)            │  │  (作用域)                   │   │
│  └─────────────────────┘  └────────────────────────────┘   │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              控制器与 Razor 页面                       │   │
│  │  - /api/v1/license/status  (License 诊断)             │   │
│  │  - /api/v1/license/refresh (强制重新验证)              │   │
│  │  - /api/v1/accounts        (账户管理)                  │   │
│  │  - /api/v1/quotes          (行情查询)                  │   │
│  │  - /api/v1/orders          (订单管理)                  │   │
│  │  - /Dashboard              (主界面)                    │   │
│  │  - /activate               (License 激活)             │   │
│  │  - /license-expired        (错误页面)                  │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                       │ HTTP POST
┌──────────────────────▼──────────────────────────────────────┐
│           LicenseForge (Azure Container Apps)                │
│  POST /api/v1/licenses/validate                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 安全部署规范

| 状态 | 规范 |
|------|------|
| 通过 | 仅本地 / 内网 / 私有 VPS 部署 |
| 通过 | 凭证仅保存在你自己的机器上 |
| 通过 | 数据不上传到第三方服务器 |
| 禁止 | 不开放公网访问 |
| 禁止 | 不使用域名对外提供服务 |
| 禁止 | 不分享 License 给他人 |

---

## 相关文档

| 文档 | 说明 |
|------|------|
| [开发者注册指南](CharlesSchwabDeveloperRegistryGuide-01.md) | Charles Schwab Developer 账户注册及 API 凭据获取步骤指南 |
| [部署指南](CharlesSchwabDeploymentGuide-02.md) | Pro Edition 详细部署说明 |
| [功能总结](SCHWAB_FEATURE_SUMMARY.md) | Community Edition Charles Schwab 集成功能完整列表 |

---

## 社区与支持

- Telegram 群组: [https://t.me/+VPy-VLis8gVmYWM1](https://t.me/+VPy-VLis8gVmYWM1)
- 商业授权与定制开发咨询: rex.fan18@gmail.com

---

> **免责声明**：See [DISCLAIMER.md](../DISCLAIMER.md) for full disclaimer and limitation of liability / 详见 [免责声明](../DISCLAIMER.md) 了解完整免责条款与责任限制。

---

**更新日期**: 2026-06-01
**版本**: 1.2.0
**维护者**: Quant.Infra.Net Team
