# Day2 + Day3 支付链路测试记录

- 项目：Saas.Infra.Net（payment 分支）
- 记录日期：2026-03-11（Asia/Shanghai）
- 测试人：Y / Codex 协同
- 测试目标：验证 Day2（Stripe 跳转支付）+ Day3（Webhook 自动化闭环）是否满足需求并可回归

## 1. 测试环境

- 应用启动方式：Visual Studio `F5`
- 应用地址：`https://localhost:7268`
- 数据库：PostgreSQL（本地/Neon 已可用）
- 测试账号：`test01@126.com / 123456`
- Stripe CLI：
  - 命令：`stripe listen --forward-to https://localhost:7268/api/payment/webhook`
  - 本轮使用的 webhook secret：`whsec_bd05f485ea15cc42299f0ac9d5f557d25a6437bbd2e879173eaaef824515e55a`
- Stripe 测试卡：
  - 成功卡：`4242 4242 4242 4242`
  - 失败卡：按 Stripe 官方失败用例卡（用于 payment failed 场景）
- Postman 环境关键变量：
  - `baseUrl=https://localhost:7268`
  - `email=test01@126.com`
  - `password=123456`
  - `clientId=infra-web`
  - `webhookSecret=whsec_...`

## 2. 覆盖范围（对应需求）

### Day2 覆盖

- 已集成 Stripe .NET SDK（已通过创建 checkout session + 跳转验证）
- 统一支付入口：`POST /api/payment/create-order` 返回 `paymentUrl`
- 校验逻辑：JWT 用户、订单归属、Pending + 未过期（通过接口和回跳行为间接验证）
- Checkout Session 创建校验：
  - metadata：`orderId/userId/priceId/productId`
  - `mode=subscription`
  - `success_url/cancel_url` 回跳正常
- 前端跳转 Stripe Hosted Page：通过 `paymentUrl` 跳转验证
- 本地 Stripe CLI webhook 转发：已验证可收到并返回 200
- 支付结果回跳页：`/checkout?payment=success|cancel|failed&orderId=...`
- 支付状态查询：`GET /api/payment/status/{orderId}`

### Day3 覆盖

- Webhook 接收：`POST /api/payment/webhook`
- 签名校验：已通过（签名错误会 401，正确后 200）
- metadata 解析 orderId：已通过（成功事件可更新对应订单）
- 幂等：已做“订单已处理则跳过”路径（日志表现正常）
- 事件处理覆盖：
  - `checkout.session.completed`（成功）
  - `checkout.session.expired`（取消/过期）
  - `payment_intent.payment_failed`（失败）
- 成功后数据闭环：
  - `Orders.Status=Paid`
  - 写入 `Transactions`
  - 创建 `Subscriptions`（按最新库结构，不含金额快照字段）
  - 订阅起止时间可用
- 返回订阅 JWT：`GET /api/payment/status/{orderId}` 中可拿到 `subscriptionAccessToken`

## 3. 执行记录（Postman Day3 Webhook + Subscription Validation）

以下用例按顺序执行，均返回 `200` 且结果符合预期：

1. `1) Login - Generate Token`：通过
2. `2) Get Product + Active Price`：通过
3. `3) Get Prices By Product`：通过
4. `4) Create Order - Success Scenario`：通过，返回 `orderId + paymentUrl`
5. `5) Webhook - checkout.session.completed`：通过
6. `6) Status - Success Should Be Paid`：通过
   - 关键断言：`orderStatus=1`，`orderStatusText=Paid`，`paid=true`，有 `subscriptionId`，有 `transactionId`
7. `7) Create Order - Expired Scenario`：通过
8. `8) Webhook - checkout.session.expired`：通过
9. `9) Status - Expired Should Be Cancelled`：通过
   - 关键断言：`orderStatus=2`，`orderStatusText=Cancelled`，`paid=false`
10. `10) Create Order - Failed Scenario`：通过
11. `11) Webhook - payment_intent.payment_failed`：通过
12. `12) Status - Failed Should Be Cancelled + Failed Tx`：通过
   - 关键断言：`orderStatus=2`，`paid=false`，`transactionStatus=2`（失败交易）

## 4. Stripe CLI 联调记录摘要

- 成功支付链路中，`checkout.session.completed`、`invoice.paid`、`invoice.payment_succeeded` 等事件均能转发到本地 webhook，并返回 `200`。
- 历史异常记录说明：
  - `connectex: actively refused`：应用未运行/端口未监听时出现，属于环境时序问题。
  - 单次 `401`：webhook secret 不一致导致，修正后恢复 `200`。
- 结论：当前环境下 Stripe CLI → 本地 Webhook 的联调已稳定。

## 5. 页面与交互验证结论

- `/checkout` 已作为“支付结果页”使用（不再是订单确认页）。
- 成功支付后可落到 Payment Result 页面并显示 Paid 状态。
- 失败卡测试提示正确。
- `My Subscriptions` 列表与状态显示正常。
- 已知小问题：`View Details` 点击存在“页面不存在”的路由问题（不影响支付主链路，但建议单独修复）。

## 6. 数据库结构变更适配确认

已按最新需求适配：

- 用户已在 DB 执行：
  - `ALTER TABLE public."Subscriptions" DROP COLUMN "OriginalAmount", DROP COLUMN "ActualAmount";`
- 代码层已同步：
  - `SubscriptionEntity` 删除金额快照字段
  - `ApplicationDbContext` 删除字段映射
  - 支付成功创建订阅逻辑不再写入上述字段
  - `SubscriptionDto` 改为从关联 `Price` 提供 `Amount`

## 7. 总结（验收结论）

- Day2、Day3 的核心目标在当前环境下均已通过实测。
- 主流程闭环已成立：
  - 登录 → 选产品/价格 → 创建订单 → 跳 Stripe → Webhook 回写 → 状态查询/订阅可见。
- 当前建议状态：**可进入提交流程**（`View Details` 路由问题建议作为后续小修复单独处理）。

## 8. 附：本轮关键接口清单

- `POST /sso/generate-token`
- `GET /api/products?activeOnly=true`
- `GET /api/prices/product/{productId}`
- `POST /api/payment/create-order`
- `POST /api/payment/webhook`
- `GET /api/payment/status/{orderId}`
- `GET /api/subscriptions`
- `GET /api/subscriptions/{subscriptionId}`
- `GET /api/subscriptions/{subscriptionId}/transactions`
