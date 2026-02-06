# Brevo SMTP 设置指南

## 问题诊断

当前您使用的是 Brevo **API Key** (`xkeysib-...`)，但是 SMTP 邮件发送需要的是 **SMTP 凭据**（用户名和密钥），这两者是不同的。

## 解决方案：获取 Brevo SMTP 凭据

### 步骤 1：登录 Brevo 账户
1. 访问 [https://app.brevo.com](https://app.brevo.com)
2. 使用您的账户登录

### 步骤 2：获取 SMTP 凭据
1. 点击右上角的头像/用户名
2. 选择 **Settings** (设置)
3. 在左侧菜单中选择 **SMTP & API**
4. 点击 **SMTP** 标签页（不是 API 标签页）

### 步骤 3：生成 SMTP 密钥
1. 在 SMTP 标签页中，点击 **"Generate a new SMTP key"** (生成新的 SMTP 密钥)
2. 输入一个名称，例如 "Quant.Infra.Net"
3. 点击 **Generate** (生成)
4. **重要**：复制显示的完整信息：
   - **SMTP 用户名** (通常是一串数字或字符)
   - **SMTP 密钥** (不是 API Key)

### 步骤 4：更新项目配置

在项目根目录执行以下命令：

```bash
# 设置 SMTP 用户名
dotnet user-secrets set "Email:Commercial:Username" "你的SMTP用户名"

# 设置 SMTP 密钥
dotnet user-secrets set "Email:Commercial:Password" "你的SMTP密钥"
```

### 步骤 5：验证配置

运行测试验证配置：

```bash
cd src/Quant.Infra.Net.Tests
dotnet test --filter "MVP_SendCommercial"
```

## 重要说明

1. **API Key vs SMTP 凭据**：
   - API Key (`xkeysib-...`) 用于 REST API 调用
   - SMTP 凭据用于 SMTP 邮件发送

2. **发件人邮箱验证**：
   - 确保 `yuanhw512@gmail.com` 已在 Brevo 中验证
   - 如果未验证，需要在 Brevo 控制台中添加并验证该邮箱

3. **SMTP 服务器设置**：
   - 服务器：`smtp-relay.brevo.com`
   - 端口：`587`
   - 加密：`STARTTLS`

## 故障排除

如果仍然无法发送邮件，请检查：

1. **SMTP 凭据是否正确**：确保复制了正确的 SMTP 用户名和密钥
2. **发件人邮箱验证**：在 Brevo 控制台验证发件人邮箱
3. **网络连接**：确保可以访问 `smtp-relay.brevo.com:587`
4. **配额限制**：检查 Brevo 账户的发送配额

## 测试成功标志

当配置正确时，您应该看到：
- ✅ 测试通过
- ✅ 控制台显示 "真实邮件已通过 Brevo 发送"
- ✅ 收件箱收到测试邮件（检查垃圾邮件文件夹）