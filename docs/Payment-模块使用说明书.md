# Payment 模块使用说明书（Saas.Infra.Net）
更新日期：2026-03-11

## 1. 模块目标
Payment 模块用于实现从“下单”到“Stripe 支付”再到“Webhook 回写”的完整闭环，并为产品端提供订阅权限信息。

## 2. 业务边界
- 下单入口：仅 API（推荐 Postman 或产品端后端调用）
- 支付页面：Stripe Hosted Checkout
- 回跳页面：`/checkout`（支付结果页）
- 支付状态查询：按 `orderId` 查询
- 订阅开通：由 Webhook 自动完成

## 3. 前置准备
### 3.1 运行环境
- 启动方式：Visual Studio `F5`
- 本地地址：`https://localhost:7268`

### 3.2 配置项
- Stripe Secret Key：`sk_test_xxx`
- Stripe Webhook Secret：`whsec_xxx`
- JWT / SSO 配置可用

### 3.3 Stripe CLI（本地联调）
```bash
stripe listen --forward-to https://localhost:7268/api/payment/webhook
```

## 4. 核心流程
### 4.1 获取产品与价格
- `GET /api/products?activeOnly=true`
- `GET /api/prices/product/{productId}`

### 4.2 创建订单（Pending）
- `POST /api/payment/create-order`
- Header：`Authorization: Bearer <JWT>`
- Body 示例：
```json
{
  "priceId": "<priceId>",
  "orderType": "SUBSCRIPTION"
}
```
返回：`orderId + paymentUrl`（Stripe Checkout 链接）

### 4.3 跳转支付
- 打开 `paymentUrl`
- 使用测试卡完成支付
  - 成功卡：`4242 4242 4242 4242`

### 4.4 Stripe Webhook 自动回写
关键事件：
- `checkout.session.completed`：支付成功
- `checkout.session.expired`：取消/超时
- `payment_intent.payment_failed`：支付失败

系统处理结果：
- 成功：`Orders=Paid`，写入 `Transactions`，创建/激活 `Subscriptions`
- 失败/过期：`Orders=Cancelled`，必要时写入失败交易

### 4.5 查询支付结果
- `GET /api/payment/status/{orderId}`
- 关键字段：
  - `orderStatus` / `orderStatusText`
  - `paid`
  - `subscriptionId`
  - `transactionStatus`
  - `subscriptionAccessToken`（成功时）

## 5. 权限与校验规则
- 用户身份从 JWT 解析
- 只能支付自己的订单
- 仅允许支付 Pending 且未过期订单
- Webhook 需校验 Stripe 签名

## 6. 返回页说明（/checkout）
- `success`：显示支付成功与订单摘要
- `cancel`：显示已取消
- `failed`：显示支付失败

说明：该页面为“支付结果页”，不再承担“创建订单”功能。

## 7. 数据库要点
- `Subscriptions` 表仅保留核心订阅字段与关联关系
- 已移除金额快照字段：`OriginalAmount` / `ActualAmount`
- 金额展示从关联 `Price` 获取

## 8. 推荐 Postman 回归顺序
1. Login Generate Token
2. Get Product + Active Price
3. Get Prices By Product
4. Create Order（Success）
5. Webhook：checkout.session.completed
6. Status 断言 Paid
7. Create Order（Expired）
8. Webhook：checkout.session.expired
9. Status 断言 Cancelled
10. Create Order（Failed）
11. Webhook：payment_intent.payment_failed
12. Status 断言 Cancelled + Failed Tx

## 9. 常见问题排查
- Stripe CLI `connect refused`：应用未启动或端口不一致
- Webhook `401`：`whsec` 配置不一致
- 回跳 `401`：登录态/JWT 丢失，请先重新登录
- `View Details` 404：前端路由未完成（不影响支付主链路）

## 10. 验收标准
- 成功支付：状态 Paid + 有订阅 + 有成功交易
- 取消支付：状态 Cancelled + 无订阅
- 失败支付：状态 Cancelled + 有失败交易
- 端到端：产品端可拿到订阅 JWT 并完成验签与权限识别
