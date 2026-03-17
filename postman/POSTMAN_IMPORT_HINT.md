# Postman 导入提示（必看）

导入 `postman` 目录下的 collection 和 environment 后，请先手动设置环境变量：

- 变量名：`webhookSecret`
- 需要填写的值：`whsec_A8KZcyTWTJXEsM9QMMtMLuv9hgAnaWf4`

填写位置：

1. 打开 Postman `Environments`
2. 选择你正在使用的环境（例如 `Day3 Webhook Validation (Azure ACA)`）
3. 找到 `webhookSecret`
4. 将值替换为上面的 `whsec_...`
5. 保存环境后再执行第 `5/8/11` 步 Webhook 请求

说明：如果 `webhookSecret` 不正确，Webhook 校验会失败（通常返回 `401`）。
