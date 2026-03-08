# 产品、价格、支付前端实施与测试步骤

> 适用项目：`Saas.Infra.MVC`
>
> 基础地址：`https://localhost:7268`

## 1. 已实施页面

## 1.1 用户端产品页面

- `GET /products` -> `Views/Products/Index.cshtml`
  - 展示激活产品列表。
  - 支持跳转到产品详情。

- `GET /products/{id}` -> `Views/Products/Details.cshtml`
  - 展示产品信息与激活价格方案。
  - 支持按价格方案跳转支付页。

## 1.2 管理端价格页面

- `GET /product-management/prices/{id}` -> `Views/ProductManagement/Prices.cshtml`
  - 价格列表展示。
  - 新增价格（调用 `POST /api/prices`）。
  - 删除价格（调用 `DELETE /api/prices/{id}`）。

## 1.3 支付页面（前端）

- `GET /checkout?priceId={priceId}` -> `Views/Checkout/Index.cshtml`
  - 调用 `POST /api/payment/create-intent` 创建支付意图。
  - 使用 Stripe Elements 完成前端支付提交。
  - 支付完成后调用 `POST /api/payment/confirm` 创建订阅与交易记录。
  - 成功后跳转：`/checkout/success?subscriptionId={id}`。

- `GET /checkout/success` -> `Views/Checkout/Success.cshtml`
  - 显示是否已完成订阅创建。
  - 若无 `subscriptionId`，提示“支付已收到但订阅待激活”。

---

## 2. 端到端测试步骤（手工）

## 2.1 前置准备

1. 登录系统，确保浏览器有 `AccessToken`（session/cookie）。
2. 数据库准备：
   - 至少一个激活产品。
   - 至少一个该产品下的激活价格。
3. `appsettings` 配置正确：
   - `Stripe:PublishableKey`
   - `Stripe:SecretKey`
   - `Stripe:WebhookSecret`（本阶段可先占位）

## 2.2 产品与价格页面测试

1. 打开 `https://localhost:7268/products`
   - 预期：出现产品卡片。
2. 点击某产品进入详情页 `/products/{id}`
   - 预期：出现价格方案按钮 `Subscribe Now`。
3. 管理端打开 `/product-management/prices/{id}`
   - 新增价格：填写表单，提交。
   - 删除价格：点击删除按钮。
   - 预期：列表刷新，新增/删除可见。

## 2.3 支付页面测试（当前实现）

1. 从产品详情点击 `Subscribe Now` 进入 `/checkout?priceId=...`
2. 页面初始化时应调用 `POST /api/payment/create-intent`
   - 预期：返回 `clientSecret` 和 `paymentIntentId`。
3. 填写 Stripe 测试卡并支付。
4. 前端应调用 `POST /api/payment/confirm`
   - 请求体包含：`paymentIntentId`、`priceId`、`gateway=Stripe`。
5. 成功后跳转 `/checkout/success?subscriptionId=...`
   - 预期：成功页显示订阅ID。
6. 打开 `/api/subscriptions/my`
   - 预期：出现新增订阅数据。

---

## 3. 支付闭环尚未完成项（缺失与待办）

当前已实现“同步确认 + 创建订阅”的主流程，但还缺少“生产级闭环”的关键能力。

## 3.1 缺失项

1. **Webhook 事件闭环处理未完成**
   - `payment_intent.succeeded` / `payment_intent.payment_failed` / `charge.refunded` 等事件未落库处理。

2. **幂等保障不足**
   - `POST /api/payment/confirm` 未做支付意图级别的幂等去重。

3. **异步支付状态回补**
   - 当用户前端中断或网络失败时，后端缺少基于 webhook 的最终状态回补。

4. **退款/撤销联动**
   - 退款后订阅状态调整、交易状态回写流程未完成。

5. **运维监控与告警**
   - 缺少支付异常、Webhook 签名失败、重复事件等监控指标与告警规则。

## 3.2 待办步骤（建议顺序）

1. 完成 `POST /api/payment/webhook` 事件分发与事件类型处理。
2. 为交易表增加唯一约束或逻辑幂等键（如 `ExternalTransactionId + Gateway`）。
3. 在 `confirm` 和 webhook 处理里统一幂等策略，避免重复创建订阅。
4. 增加支付状态机（Pending/Success/Failed/Refunded）并统一更新入口。
5. 增加“订阅激活延迟”页面提示与前端轮询状态接口。
6. 增加自动化测试：
   - 单元测试（支付状态机、幂等分支）
   - 集成测试（模拟 webhook 回调）
7. 接入日志聚合与告警（支付失败率、Webhook失败率、重复事件率）。

---

## 4. 验收标准（当前阶段）

满足以下即视为“前端可联调版本”完成：

1. 产品列表/详情可正常访问。
2. 管理端价格增删可用。
3. Checkout 页面可创建支付意图。
4. 前端支付成功后能调用 `confirm` 并跳转成功页。
5. `/api/subscriptions/my` 可查到新增订阅。
6. 成功页在无订阅ID时能明确提示“待激活”。
