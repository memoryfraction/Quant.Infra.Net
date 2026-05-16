# Quant.Infra.Net 邮件服务架构文档

## 📐 架构概览

当前邮件服务采用**策略模式 + 工厂模式**的设计，支持多种邮件发送方式的灵活切换。

```
┌─────────────────────────────────────────────────────────────┐
│                      应用层 (Application)                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │  ASP.NET API │  │  Console App │  │  Test Project │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
└─────────┼──────────────────┼──────────────────┼─────────────┘
          │                  │                  │
          └──────────────────┼──────────────────┘
                             │
┌────────────────────────────┼─────────────────────────────────┐
│                   服务层 (Service Layer)                      │
│                            │                                  │
│              ┌─────────────▼──────────────┐                  │
│              │  EmailServiceFactory       │                  │
│              │  (工厂模式 - 服务路由)      │                  │
│              └─────────────┬──────────────┘                  │
│                            │                                  │
│              ┌─────────────┴──────────────┐                  │
│              │                            │                  │
│    ┌─────────▼──────────┐    ┌──────────▼──────────┐       │
│    │ PersonalEmailService│    │CommercialEmailService│       │
│    │  (个人邮箱服务)      │    │  (商业邮件服务)      │       │
│    │  - 126邮箱          │    │  - Brevo SMTP        │       │
│    │  - QQ邮箱           │    │  - SendGrid (扩展)   │       │
│    │  - Gmail            │    │  - AWS SES (扩展)    │       │
│    └─────────┬──────────┘    └──────────┬──────────┘       │
│              │                            │                  │
│              └─────────────┬──────────────┘                  │
│                            │                                  │
│              ┌─────────────▼──────────────┐                  │
│              │      IEmailService         │                  │
│              │  (统一接口 - 策略模式)     │                  │
│              └────────────────────────────┘                  │
└──────────────────────────────────────────────────────────────┘
                             │
┌────────────────────────────┼─────────────────────────────────┐
│                   模型层 (Model Layer)                        │
│                            │                                  │
│    ┌───────────────────────▼────────────────────┐           │
│    │         EmailSettingBase (抽象基类)         │           │
│    │  - SmtpServer, Port, SenderEmail           │           │
│    │  - Username, Password, SenderName          │           │
│    └───────────────────┬────────────────────────┘           │
│                        │                                      │
│         ┌──────────────┴──────────────┐                     │
│         │                              │                     │
│  ┌──────▼──────────┐        ┌─────────▼──────────┐         │
│  │PersonalEmailSetting│      │CommercialEmailSetting│        │
│  │  (个人邮箱配置)   │        │  (商业邮件配置)     │        │
│  └───────────────────┘        └────────────────────┘         │
│                                                               │
│    ┌──────────────────────────────────────────┐             │
│    │         EmailMessage (邮件消息)           │             │
│    │  - To: List<string> (收件人列表)         │             │
│    │  - Subject: string (主题)                │             │
│    │  - Body: string (正文)                   │             │
│    │  - IsHtml: bool (是否HTML格式)           │             │
│    └──────────────────────────────────────────┘             │
└──────────────────────────────────────────────────────────────┘
                             │
┌────────────────────────────┼─────────────────────────────────┐
│                   基础设施层 (Infrastructure)                 │
│                            │                                  │
│              ┌─────────────▼──────────────┐                  │
│              │      MailKit Library       │                  │
│              │  - SmtpClient              │                  │
│              │  - MimeMessage             │                  │
│              │  - BodyBuilder             │                  │
│              └────────────────────────────┘                  │
└──────────────────────────────────────────────────────────────┘
```

## 🏗️ 核心组件

### 1. **接口层 (IEmailService)**
```csharp
public interface IEmailService
{
    Task<bool> SendBulkEmailAsync(EmailMessage message, EmailSettingBase setting);
}
```
- **职责**：定义邮件服务的统一契约
- **优势**：支持依赖注入，便于单元测试和服务替换

### 2. **工厂层 (EmailServiceFactory)**
```csharp
public class EmailServiceFactory
{
    public IEmailService GetService(int recipientCount)
    {
        // 根据配置和收件人数量返回合适的服务
    }
}
```
- **职责**：根据配置策略选择合适的邮件服务
- **路由逻辑**：
  - `Email:Type = "Personal"` → PersonalEmailService
  - `Email:Type = "Commercial"` → CommercialEmailService
- **扩展性**：未来可添加更多路由规则（如根据收件人数量自动选择）

### 3. **服务实现层**

#### PersonalEmailService (个人邮箱服务)
- **适用场景**：小规模邮件发送（< 50 封）
- **支持邮箱**：126、QQ、Gmail、Outlook 等
- **特点**：
  - 使用 SSL/TLS 加密（端口 465/587）
  - 支持 HTML 和纯文本格式
  - 批量发送时自动添加延迟（防止限流）

#### CommercialEmailService (商业邮件服务)
- **适用场景**：大规模邮件发送（≥ 50 封）
- **当前集成**：Brevo (SendinBlue)
- **特点**：
  - 使用 SMTP 中继服务
  - 支持 STARTTLS 加密
  - 详细的日志记录
  - 智能错误提示（区分 API Key 和 SMTP 凭据）
  - 环境感知（开发/生产环境不同行为）

### 4. **模型层**

#### EmailSettingBase (配置基类)
```csharp
public abstract class EmailSettingBase
{
    public string SmtpServer { get; set; }
    public int Port { get; set; }
    public string SenderEmail { get; set; }
    public string SenderName { get; set; }
    public string Username { get; set; }      // SMTP 用户名
    public string Password { get; set; }      // SMTP 密码/密钥
}
```

#### EmailMessage (邮件消息)
```csharp
public class EmailMessage
{
    public List<string> To { get; set; }      // 收件人列表
    public string Subject { get; set; }       // 邮件主题
    public string Body { get; set; }          // 邮件正文
    public bool IsHtml { get; set; }          // 是否 HTML 格式
}
```

## 🔧 配置管理

### appsettings.json 配置结构
```json
{
  "Email": {
    "Type": "Commercial",  // 或 "Personal"
    "Personal": {
      "SmtpServer": "smtp.126.com",
      "Port": "465",
      "SenderEmail": "your@126.com",
      "SenderName": "Your Name"
    },
    "Commercial": {
      "SmtpServer": "smtp-relay.brevo.com",
      "Port": "587",
      "SenderEmail": "your@gmail.com",
      "SenderName": "Your Company"
    }
  }
}
```

### 用户机密 (User Secrets) 配置
```bash
# 个人邮箱密码
dotnet user-secrets set "Email:Personal:Password" "your-email-password"

# Brevo SMTP 凭据
dotnet user-secrets set "Email:Commercial:Username" "your-smtp-username"
dotnet user-secrets set "Email:Commercial:Password" "your-smtp-key"
```

## 🎯 设计模式应用

### 1. **策略模式 (Strategy Pattern)**
- **目的**：定义一系列算法（邮件发送策略），让它们可以互相替换
- **实现**：
  - `IEmailService` 是策略接口
  - `PersonalEmailService` 和 `CommercialEmailService` 是具体策略
- **优势**：
  - 易于添加新的邮件服务提供商
  - 运行时可动态切换策略
  - 符合开闭原则（对扩展开放，对修改关闭）

### 2. **工厂模式 (Factory Pattern)**
- **目的**：封装对象创建逻辑，根据条件返回不同的实例
- **实现**：`EmailServiceFactory` 根据配置返回合适的服务
- **优势**：
  - 客户端代码不需要知道具体实现类
  - 集中管理服务创建逻辑
  - 便于添加新的创建规则

### 3. **依赖注入 (Dependency Injection)**
- **目的**：降低组件间的耦合度
- **实现**：
  ```csharp
  services.AddTransient<PersonalEmailService>();
  services.AddTransient<CommercialEmailService>();
  services.AddTransient<EmailServiceFactory>();
  ```
- **优势**：
  - 便于单元测试（可注入 Mock 对象）
  - 提高代码可维护性
  - 支持生命周期管理

## 📊 数据流

### 典型邮件发送流程
```
1. 应用层创建 EmailMessage
   ↓
2. 从配置读取 EmailSettings
   ↓
3. 通过 EmailServiceFactory 获取服务
   ↓
4. 调用 IEmailService.SendBulkEmailAsync()
   ↓
5. 服务实现连接 SMTP 服务器
   ↓
6. 身份验证
   ↓
7. 构建 MimeMessage
   ↓
8. 发送邮件
   ↓
9. 返回发送结果 (bool)
```

## 🔐 安全性设计

### 1. **凭据管理**
- ✅ 使用 User Secrets 存储敏感信息（开发环境）
- ✅ 支持环境变量（生产环境）
- ✅ 不在代码中硬编码密码
- ✅ 配置文件中不包含真实凭据

### 2. **传输安全**
- ✅ 个人邮箱：SSL/TLS 加密（端口 465）
- ✅ 商业邮件：STARTTLS 加密（端口 587）
- ✅ 验证 SMTP 服务器证书

### 3. **错误处理**
- ✅ 详细的错误日志
- ✅ 智能错误提示（区分不同类型的错误）
- ✅ 异常捕获和优雅降级

## 🚀 扩展性

### 当前支持的扩展点

1. **添加新的邮件服务提供商**
   ```csharp
   public class SendGridEmailService : IEmailService
   {
       public async Task<bool> SendBulkEmailAsync(EmailMessage message, EmailSettingBase setting)
       {
           // SendGrid 实现
       }
   }
   ```

2. **自定义路由规则**
   ```csharp
   public IEmailService GetService(int recipientCount)
   {
       if (recipientCount < 50)
           return _serviceProvider.GetRequiredService<PersonalEmailService>();
       else
           return _serviceProvider.GetRequiredService<CommercialEmailService>();
   }
   ```

3. **添加邮件模板系统**
   - 已提供 `EmailTemplates.cs` 示例
   - 支持动态内容替换
   - 支持多种预定义模板

### 未来可能的扩展

- [ ] 支持附件发送
- [ ] 支持抄送/密送
- [ ] 邮件发送队列（异步处理）
- [ ] 发送失败重试机制
- [ ] 邮件发送统计和监控
- [ ] 支持更多邮件服务商（AWS SES, SendGrid, Mailgun）
- [ ] 邮件模板引擎集成（Razor, Liquid）

## 📝 使用示例

### 基本使用
```csharp
// 1. 配置 DI
services.AddTransient<PersonalEmailService>();
services.AddTransient<CommercialEmailService>();
services.AddTransient<EmailServiceFactory>();

// 2. 创建邮件消息
var message = new EmailMessage
{
    To = new List<string> { "user@example.com" },
    Subject = "测试邮件",
    Body = "<h1>Hello World</h1>",
    IsHtml = true
};

// 3. 配置邮件设置
var settings = new CommercialEmailSetting
{
    SmtpServer = "smtp-relay.brevo.com",
    Port = 587,
    SenderEmail = "sender@example.com",
    SenderName = "My App",
    Username = "smtp-username",
    Password = "smtp-password"
};

// 4. 发送邮件
var factory = serviceProvider.GetRequiredService<EmailServiceFactory>();
var service = factory.GetService(message.To.Count);
var result = await service.SendBulkEmailAsync(message, settings);
```

## 🧪 测试策略

### 单元测试
- 使用 Mock 对象模拟 SMTP 服务器
- 测试不同配置下的服务选择逻辑
- 测试错误处理和边界条件

### 集成测试
- 使用真实的 SMTP 服务器
- 验证邮件实际发送成功
- 测试不同邮件服务提供商的兼容性

### 当前测试覆盖
- ✅ PersonalEmailService 基本发送
- ✅ CommercialEmailService (Brevo) 真实发送
- ✅ EmailServiceFactory 路由逻辑
- ✅ 配置加载和用户机密集成

## 📚 相关文档

- [使用指南](USAGE_EXAMPLES.md) - 详细的使用示例和代码片段
- [Brevo 设置指南](BREVO_SMTP_SETUP_GUIDE.md) - Brevo SMTP 配置步骤
- [邮件模板](EmailTemplates.cs) - 预定义的邮件模板
- [使用示例类](EmailService_Usage_Example.cs) - 封装好的邮件服务类

## 🎓 架构优势总结

1. **灵活性**：通过工厂模式和策略模式，轻松切换不同的邮件服务
2. **可扩展性**：遵循开闭原则，添加新服务无需修改现有代码
3. **可测试性**：依赖注入使得单元测试更容易
4. **安全性**：凭据管理和传输加密保证安全
5. **可维护性**：清晰的分层架构，职责明确
6. **生产就绪**：详细的日志、错误处理和环境感知

## 📞 技术栈

- **.NET 8.0** - 运行时框架
- **MailKit** - SMTP 客户端库
- **MimeKit** - 邮件消息构建
- **Microsoft.Extensions.Configuration** - 配置管理
- **Microsoft.Extensions.DependencyInjection** - 依赖注入
- **MSTest** - 单元测试框架

---

**最后更新**: 2026-04-19  
**版本**: 1.0.0  
**维护者**: Quant.Infra.Net Team
