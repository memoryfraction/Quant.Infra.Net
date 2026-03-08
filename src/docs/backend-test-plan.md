# 后端联调测试文档（前端未完成版）

> 适用项目：`Saas.Infra.MVC`
> 
> 基础地址：`https://localhost:7268`
> 
> Swagger：`https://localhost:7268/swagger`

## 1. 测试目标

在前端未完成时，先验证后端以下能力：

- SSO 登录与 JWT 签发
- 产品管理 API
- 价格管理 API
- 支付 API（Stripe）
- 订阅 API

---

## 2. 环境准备

1. 启动 `Saas.Infra.MVC`。
2. 检查 `Saas.Infra.MVC/appsettings.json`：
   - `Jwt:PrivateKeyPath` 和 `Jwt:PublicKeyPath` 文件存在。
   - `Jwt:Issuer`、`Jwt:Audience` 与服务端验证一致。
   - `ConnectionStrings:DefaultConnection` 已配置（当前文件默认是空字符串，需改为有效连接串）。
   - 如测试支付：`Stripe:SecretKey`、`Stripe:PublishableKey`、`Stripe:WebhookSecret` 有效。
3. 数据库准备：
   - 存在可登录用户（邮箱+密码）。
   - 至少有一个产品和一个价格（也可按第 4 节接口创建）。

---

## 3. 快速联调流程（推荐顺序）

1. 先调用 `POST /sso/generate-token` 拿 `accessToken`。
2. 在 Swagger `Authorize` 中填 `Bearer {accessToken}`。
3. 测试产品接口 -> 价格接口 -> 支付接口 -> 订阅接口。

---

## 4. 接口测试步骤

## 4.1 生成 Token

- 方法：`POST`
- 路径：`/sso/generate-token`
- Body：

```json
{
  "email": "test@126.com",
  "password": "123456",
  "clientId": "swagger"
}
```

- 预期：
  - 成功 `200`：返回 `accessToken`、`refreshToken`、`expiresIn`
  - 失败 `401`：账号密码错误
  - 失败 `500`：系统异常（见第 6 节排查）

---

## 4.2 产品接口

### 获取产品列表

- 方法：`GET`
- 路径：`/api/Products`
- Header：`Authorization: Bearer {accessToken}`

### 创建产品（管理员）

- 方法：`POST`
- 路径：`/api/Products`
- Body：

```json
{
  "code": "PRO_PLAN",
  "name": "Pro Plan",
  "description": "Pro product",
  "isActive": true
}
```

- 预期：`201`，返回产品对象（含 `id`）

---

## 4.3 价格接口

### 创建价格（管理员）

- 方法：`POST`
- 路径：`/api/Prices`
- Body：

```json
{
  "productId": "{上一步产品ID}",
  "name": "Monthly",
  "billingPeriod": "month",
  "amount": 999,
  "currency": "USD",
  "isActive": true
}
```

- 预期：`201`，返回价格对象（含 `id`）

### 查询价格

- `GET /api/Prices/product/{productId}`
- `GET /api/Prices/{priceId}`

---

## 4.4 支付接口

### 创建支付意图

- 方法：`POST`
- 路径：`/api/Payment/create-intent`
- Body：

```json
{
  "priceId": "{价格ID}",
  "gateway": "Stripe"
}
```

### 确认支付

- 方法：`POST`
- 路径：`/api/Payment/confirm`
- Body：

```json
{
  "paymentIntentId": "{create-intent返回ID}",
  "priceId": "{价格ID}",
  "gateway": "Stripe"
}
```

- 预期：`200`，`success=true` 且返回 `subscriptionId`

---

## 4.5 订阅接口

- `GET /api/Subscriptions/my`
- `GET /api/Subscriptions/{id}`
- `POST /api/Subscriptions/{id}/cancel`
- `GET /api/Subscriptions/{id}/transactions`

> 注意：当前代码通过 `ClaimTypes.NameIdentifier` 获取用户 ID，若 Token 中无该 Claim，部分接口可能返回未授权或无法关联用户。

---

## 5. 通用断言清单

每次请求至少检查：

1. 状态码是否符合预期（`200/201/400/401/403/404/500`）。
2. 响应字段是否完整（如 `accessToken`、`id`、`subscriptionId`）。
3. 日志中无未处理异常（`[ERR]`）。
4. 数据库数据变化是否符合预期（产品、价格、交易、订阅）。

---

## 6. 当前已知问题与排查（你日志里的 500）

你当前现象：`POST /sso/generate-token` 返回 `500`，日志提示 `Unexpected error during RSA token generation`。

建议按顺序排查：

1. **数据库连接**
   - 当前 `appsettings.json` 中 `DefaultConnection` 为空。
   - 若未配置，用户查询会失败并触发 500。

2. **用户数据**
   - 确认 `test@126.com` 用户存在。
   - 确认密码哈希与输入密码匹配。

3. **JWT RSA 密钥路径**
   - `Secrets/sso_rsa_private.pem`、`PublicKeys/sso_rsa_public.pem` 文件存在。
   - 文件内容格式正确（PEM）。

4. **Issuer/Audience 一致性**
   - `Token` 签发与验证使用的 `Issuer/Audience` 一致。

---

## 7. cURL 示例（可直接跑）

```bash
curl -k -X POST "https://localhost:7268/sso/generate-token" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@126.com","password":"123456","clientId":"swagger"}'
```

```bash
curl -k "https://localhost:7268/api/Products" \
  -H "Authorization: Bearer {accessToken}"
```

```bash
curl -k -X POST "https://localhost:7268/api/Prices" \
  -H "Authorization: Bearer {accessToken}" \
  -H "Content-Type: application/json" \
  -d '{"productId":"{productId}","name":"Monthly","billingPeriod":"month","amount":999,"currency":"USD","isActive":true}'
```

---

## 8. 交付建议

前端完成前，可先把这份文档作为后端联调基线。后续可再补：

- Postman Collection
- Postman Environment（本地 / 测试环境）
- 自动化 API 回归脚本
