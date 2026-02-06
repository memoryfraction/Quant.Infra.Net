# Quant.Infra.Net 邮件服务使用指南

## 在其他项目中调用 Brevo 邮件服务

### 1. 添加项目引用

在您的项目中添加对 `Quant.Infra.Net` 的引用：

```xml
<ProjectReference Include="path/to/Quant.Infra.Net/Quant.Infra.Net.csproj" />
```

### 2. 配置用户机密或配置文件

#### 方法 A：使用用户机密（推荐用于开发环境）

```bash
# 在您的项目根目录执行
dotnet user-secrets init
dotnet user-secrets set "Email:Commercial:Username" "your-smtp-username"
dotnet user-secrets set "Email:Commercial:Password" "your-smtp-key-here"
```

#### 方法 B：使用 appsettings.json（生产环境需要加密）

```json
{
  "Email": {
    "Commercial": {
      "SmtpServer": "smtp-relay.brevo.com",
      "Port": "587",
      "SenderEmail": "yuanhw512@gmail.com",
      "SenderName": "Your App Name",
      "Username": "a136bf001@smtp-brevo.com",
      "Password": "your-smtp-key-here"
    }
  }
}
```

### 3. 代码示例

#### 示例 1：直接使用 CommercialService

```csharp
using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Notification.Service;
using Microsoft.Extensions.Configuration;

public class EmailHelper
{
    private readonly IConfiguration _configuration;

    public EmailHelper(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<bool> SendBrevoEmailAsync(
        List<string> recipients, 
        string subject, 
        string htmlBody, 
        string senderName = "Your App")
    {
        // 创建邮件消息
        var message = new EmailMessage
        {
            To = recipients,
            Subject = subject,
            Body = htmlBody,
            IsHtml = true
        };

        // 从配置中读取 Brevo 设置
        var commercialConfig = _configuration.GetSection("Email:Commercial");
        var settings = new EmailSettings
        {
            SmtpServer = commercialConfig["SmtpServer"] ?? "smtp-relay.brevo.com",
            Port = int.Parse(commercialConfig["Port"] ?? "587"),
            SenderEmail = commercialConfig["SenderEmail"] ?? "yuanhw512@gmail.com",
            SenderName = commercialConfig["SenderName"] ?? senderName,
            Username = commercialConfig["Username"] ?? throw new InvalidOperationException("Brevo SMTP Username not found"),
            Password = commercialConfig["Password"] ?? throw new InvalidOperationException("Brevo SMTP Key not found")
        };

        // 发送邮件
        var service = new CommercialService();
        return await service.SendBulkEmailAsync(message, settings);
    }
}
```

#### 示例 2：使用 EmailServiceFactory（智能路由）

```csharp
using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Notification.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class EmailManager
{
    private readonly EmailServiceFactory _factory;
    private readonly IConfiguration _configuration;

    public EmailManager(IConfiguration configuration)
    {
        _configuration = configuration;
        
        // 设置 DI 容器
        var services = new ServiceCollection();
        services.AddTransient<PersonalEmailService>();
        services.AddTransient<CommercialService>();
        services.AddSingleton(configuration);
        
        var serviceProvider = services.BuildServiceProvider();
        _factory = new EmailServiceFactory(serviceProvider);
    }

    public async Task<bool> SendEmailAsync(
        List<string> recipients, 
        string subject, 
        string htmlBody, 
        string senderName = "Your App")
    {
        // 创建邮件消息
        var message = new EmailMessage
        {
            To = recipients,
            Subject = subject,
            Body = htmlBody,
            IsHtml = true
        };

        // 根据收件人数量自动选择服务
        // < 50 个收件人：使用 PersonalEmailService
        // >= 50 个收件人：使用 CommercialService (Brevo)
        var service = _factory.GetService(recipients.Count);

        EmailSettings settings;
        
        if (service is CommercialService)
        {
            // Brevo 商业服务配置
            var commercialConfig = _configuration.GetSection("Email:Commercial");
            settings = new EmailSettings
            {
                SmtpServer = commercialConfig["SmtpServer"] ?? "smtp-relay.brevo.com",
                Port = int.Parse(commercialConfig["Port"] ?? "587"),
                SenderEmail = commercialConfig["SenderEmail"] ?? "yuanhw512@gmail.com",
                SenderName = commercialConfig["SenderName"] ?? senderName,
                Username = commercialConfig["Username"] ?? throw new InvalidOperationException("Brevo SMTP Username not found"),
                Password = commercialConfig["Password"] ?? throw new InvalidOperationException("Brevo SMTP Key not found")
            };
        }
        else
        {
            // 个人邮件服务配置（126邮箱等）
            var personalConfig = _configuration.GetSection("Email:Personal");
            settings = new EmailSettings
            {
                SmtpServer = personalConfig["SmtpServer"] ?? "smtp.126.com",
                Port = int.Parse(personalConfig["Port"] ?? "465"),
                SenderEmail = personalConfig["SenderEmail"] ?? throw new InvalidOperationException("Personal email not configured"),
                SenderName = personalConfig["SenderName"] ?? senderName,
                Username = personalConfig["SenderEmail"], // 个人邮箱用户名通常是邮箱地址
                Password = personalConfig["Password"] ?? throw new InvalidOperationException("Personal email password not found")
            };
        }

        return await service.SendBulkEmailAsync(message, settings);
    }
}
```

### 4. 在 ASP.NET Core 中使用

#### Startup.cs 或 Program.cs 配置

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // 注册邮件服务
    services.AddTransient<PersonalEmailService>();
    services.AddTransient<CommercialService>();
    services.AddTransient<EmailServiceFactory>();
    services.AddTransient<EmailHelper>();
    services.AddTransient<EmailManager>();
}
```

#### Controller 中使用

```csharp
[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly EmailHelper _emailHelper;

    public EmailController(EmailHelper emailHelper)
    {
        _emailHelper = emailHelper;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
    {
        try
        {
            var result = await _emailHelper.SendBrevoEmailAsync(
                request.Recipients,
                request.Subject,
                request.HtmlBody,
                request.SenderName
            );

            if (result)
            {
                return Ok(new { success = true, message = "邮件发送成功" });
            }
            else
            {
                return BadRequest(new { success = false, message = "邮件发送失败" });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}

public class SendEmailRequest
{
    public List<string> Recipients { get; set; } = new();
    public string Subject { get; set; } = "";
    public string HtmlBody { get; set; } = "";
    public string SenderName { get; set; } = "Your App";
}
```

### 5. 简单的控制台应用示例

```csharp
using Microsoft.Extensions.Configuration;
using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Notification.Service;

class Program
{
    static async Task Main(string[] args)
    {
        // 构建配置
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<Program>()
            .Build();

        // 创建邮件助手
        var emailHelper = new EmailHelper(configuration);

        // 发送邮件
        var recipients = new List<string> 
        { 
            "yuanyuancomecome@outlook.com", 
            "rong.fan1031@gmail.com" 
        };

        var result = await emailHelper.SendBrevoEmailAsync(
            recipients,
            "测试邮件",
            "<h1>Hello from Quant.Infra.Net!</h1><p>这是一封测试邮件。</p>",
            "My Console App"
        );

        Console.WriteLine(result ? "邮件发送成功！" : "邮件发送失败！");
    }
}
```

### 6. 最佳实践

1. **安全性**：
   - 生产环境中使用环境变量或 Azure Key Vault 存储 SMTP 凭据
   - 不要将 SMTP 密钥提交到代码仓库

2. **错误处理**：
   - 始终使用 try-catch 包装邮件发送代码
   - 记录详细的错误日志

3. **性能优化**：
   - 对于大量邮件，考虑使用后台任务队列
   - 使用 CommercialService 进行批量发送

4. **配置管理**：
   - 使用强类型配置类
   - 支持多环境配置

### 7. 故障排除

如果邮件发送失败，检查：
1. SMTP 凭据是否正确
2. 发件人邮箱是否在 Brevo 中验证
3. 网络连接是否正常
4. Brevo 账户配额是否充足