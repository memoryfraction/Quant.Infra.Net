# Saas.Infra.Net

基于 .NET 10 的 SaaS 基础设施框架，提供 SSO 认证、支付集成、基于角色的访问控制和 Blazor Server UI —— 旨在为构建 SaaS 应用提供可复用的基础架构。

A .NET 10 SaaS infrastructure framework providing SSO authentication, payment integration, role-based access control, and Blazor Server UI — designed as a reusable foundation for building SaaS applications.

---

## 功能特性 / Features

- **SSO & JWT 认证 / SSO & JWT Authentication** — 基于 RSA 签名的 JWT 令牌签发、刷新、撤销与验证，通过自定义中间件管道实现。RSA-signed JWT token issuance, refresh, revocation, and validation via a custom middleware pipeline.
- **基于角色的访问控制 (RBAC) / Role-Based Access Control** — 内置角色（`SUPER_ADMIN`、`ADMIN`、`USER`），支持基于特性的授权。Built-in roles with attribute-based authorization.
- **Stripe 支付集成 / Stripe Payment Integration** — 完整支付生命周期：创建订单 → Stripe Checkout → Webhook 回调 → 订阅管理。Full payment lifecycle: order creation → Stripe Checkout → Webhook callbacks → subscription management.
- **Blazor Server UI** — 交互式服务端渲染 UI，包含账户管理、仪表盘、产品管理、管理后台和结账页面。Interactive server-rendered UI with pages for account management, dashboard, product management, admin panels, and checkout.
- **安全加固 / Security Hardened** — 安全头中间件（CSP、HSTS、X-Frame-Options）、CSRF 防护、BCrypt 密码哈希、全局异常处理。Security headers middleware, CSRF protection, BCrypt password hashing, and global exception handling.
- **Serilog 日志 / Serilog Logging** — 双输出（控制台 + 滚动文件），全应用结构化日志。Dual output (console + rolling file) with structured logging throughout the application.
- **PostgreSQL + EF Core** — 使用 Entity Framework Core 和 Npgsql 提供数据持久化。Data persistence with Entity Framework Core and Npgsql provider.

---

## 解决方案结构 / Solution Structure

```
Saas.Infra.Net/
├── src/
│   ├── Saas.Infra.Core          # 领域模型、接口、枚举、JWT 选项 / Domain models, interfaces, enums, JWT options
│   ├── Saas.Infra.Data          # EF Core DbContext、实体、仓储 (PostgreSQL) / EF Core DbContext, entities, repositories
│   ├── Saas.Infra.SSO           # SSO 服务、令牌服务、BCrypt 密码哈希 / SSO service, token service, BCrypt password hasher
│   ├── Saas.Infra.MVC           # Blazor Server 应用、API 控制器、中间件、支付服务 / Blazor Server app, API controllers, middleware, payment services
│   ├── Saas.Infra.Payment       # 支付模块（预留） / Payment module (reserved)
│   ├── Saas.Infra.Products      # 产品模块（预留） / Products module (reserved)
│   └── Saas.Infra.Net.Tests     # 单元测试 (MSTest) / Unit tests (MSTest)
├── docs/                        # 文档和测试记录 / Documentation and test records
├── postman/                     # API 测试 Postman 集合 / Postman collection for API testing
└── images/                      # 图片资源 / Image assets
```

### 项目依赖 / Project Dependencies

```
Saas.Infra.MVC
  ├── Saas.Infra.SSO
  │     └── Saas.Infra.Core
  └── Saas.Infra.Data
        └── Saas.Infra.Core
```

---

## 技术栈 / Tech Stack

| 类别 / Category | 技术 / Technology |
|---|---|
| 运行时 / Runtime | .NET 10 |
| Web 框架 / Web Framework | ASP.NET Core (Blazor Server + Web API) |
| 认证 / Authentication | RSA 签名 JWT（自定义中间件）/ RSA-signed JWT (custom middleware) |
| 密码哈希 / Password Hashing | BCrypt.Net-Next |
| 数据库 / Database | PostgreSQL |
| ORM | Entity Framework Core 9 + Npgsql |
| 对象映射 / Object Mapping | Mapster |
| 支付网关 / Payment Gateway | Stripe.net |
| 日志 / Logging | Serilog（控制台 + 文件）/ Serilog (Console + File sinks) |
| API 文档 / API Documentation | Swashbuckle (Swagger) |
| 测试 / Testing | MSTest |

---

## 快速开始 / Getting Started

### 前置条件 / Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 数据库实例 / PostgreSQL database instance
- Stripe 账户（用于支付功能）/ Stripe account (for payment features)

### 配置 / Configuration

1. **数据库 / Database** — 在 `appsettings.json` 或用户机密中设置 PostgreSQL 连接字符串：Set the PostgreSQL connection string in `appsettings.json` or user secrets:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=saas_infra;Username=postgres;Password=yourpassword"
     }
   }
   ```

2. **RSA 密钥 / RSA Keys** — 放置用于 JWT 签名的 RSA 密钥对：Place your RSA key pair for JWT signing:
   - 私钥 / Private key: `Saas.Infra.MVC/Secrets/sso_rsa_private.pem`
   - 公钥 / Public key: `Saas.Infra.MVC/PublicKeys/sso_rsa_public.pem`

3. **Stripe** — 在用户机密或 `appsettings.json` 中配置 Stripe 密钥：Configure Stripe keys in user secrets or `appsettings.json`:

   ```json
   {
     "Stripe": {
       "PublishableKey": "pk_test_xxx",
       "SecretKey": "sk_test_xxx",
       "WebhookSecret": "whsec_xxx"
     }
   }
   ```

4. **JWT** — 在 `appsettings.json` 中配置 JWT 参数：JWT settings in `appsettings.json`:

   ```json
   {
     "Jwt": {
       "Issuer": "Saas.Infra.SSO",
       "Audience": "Saas.Infra.Clients",
       "AccessTokenExpirationMinutes": 60,
       "PrivateKeyPath": "Secrets/sso_rsa_private.pem",
       "PublicKeyPath": "PublicKeys/sso_rsa_public.pem"
     }
   }
   ```

### 运行 / Run

```bash
cd src
dotnet run --project Saas.Infra.MVC
```

应用默认启动于 `https://localhost:7268`，Swagger UI 位于 `/swagger`。

The application starts at `https://localhost:7268` by default. Swagger UI is available at `/swagger`.

### 运行测试 / Run Tests

```bash
cd src
dotnet test
```

---

## API 端点 / API Endpoints

### SSO / 认证 Authentication

| 方法 / Method | 端点 / Endpoint | 描述 / Description |
|---|---|---|
| POST | `/sso/login` | 用户登录，返回 JWT 令牌 / User login, returns JWT tokens |
| POST | `/sso/register` | 注册新用户 / Register new user |
| POST | `/sso/refresh` | 刷新访问令牌 / Refresh access token |
| POST | `/sso/revoke` | 撤销刷新令牌（登出）/ Revoke refresh token (logout) |

### 用户管理 / User Management

| 方法 / Method | 端点 / Endpoint | 描述 / Description |
|---|---|---|
| GET | `/api/users/me` | 获取当前用户资料 / Get current user profile |
| PUT | `/api/users/change-password` | 修改密码 / Change password |

### 产品与价格 / Products & Prices

| 方法 / Method | 端点 / Endpoint | 描述 / Description |
|---|---|---|
| GET | `/api/products` | 产品列表 / List products |
| GET | `/api/prices/product/{productId}` | 获取产品价格 / Get prices for a product |

### 支付 / Payment

| 方法 / Method | 端点 / Endpoint | 描述 / Description |
|---|---|---|
| POST | `/api/payment/create-order` | 创建支付订单 / Create a payment order |
| POST | `/api/payment/create-intent` | 创建支付意图 / Create a payment intent |
| GET | `/api/payment/status/{orderId}` | 查询支付状态 / Query payment status |
| POST | `/api/payment/webhook` | Stripe Webhook 端点 / Stripe webhook endpoint |

---

### Blazor 页面 / Blazor Pages

| 路由 / Route | 描述 / Description |
|---|---|
| `/account/login` | 登录页 / Login page |
| `/account/register` | 注册页 / Registration page |
| `/dashboard` | 用户仪表盘 / User dashboard |
| `/products` | 产品列表 / Product listing |
| `/checkout` | 支付结果页 / Payment result page |
| `/admin` | 管理后台 / Admin panel |
| `/superadmin` | 超级管理后台 / Super admin panel |

---

## 支付流程 / Payment Workflow

1. **浏览产品 / Browse Products** → `GET /api/products` + `GET /api/prices/product/{id}`
2. **创建订单 / Create Order** → `POST /api/payment/create-order` → 返回 `orderId` + `paymentUrl` / returns `orderId` + `paymentUrl`
3. **跳转 Stripe 结账 / Redirect to Stripe Checkout** → 用户在 Stripe 托管页面完成支付 / User completes payment on Stripe-hosted page
4. **Webhook 回调 / Webhook Callback** → Stripe 发送 `checkout.session.completed` → 系统更新订单状态、创建交易记录、激活订阅 / system updates order status, creates transaction, activates subscription
5. **查询状态 / Query Status** → `GET /api/payment/status/{orderId}` → 确认支付和订阅详情 / confirms payment and subscription details

---

## 许可证 / License

本项目基于 [CC0 1.0 通用](LICENSE) 许可证发布。

This project is licensed under [CC0 1.0 Universal](LICENSE).
