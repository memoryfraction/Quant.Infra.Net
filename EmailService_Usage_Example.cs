using Microsoft.Extensions.Configuration;
using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Notification.Service;

namespace YourProject.Services
{
    /// <summary>
    /// 邮件服务封装类 - 在您的项目中使用
    /// </summary>
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// 发送单个邮件
        /// </summary>
        public async Task<bool> SendEmailAsync(string recipient, string subject, string htmlBody, string? senderName = null)
        {
            return await SendEmailAsync(new List<string> { recipient }, subject, htmlBody, senderName);
        }

        /// <summary>
        /// 发送批量邮件（使用 Brevo）
        /// </summary>
        public async Task<bool> SendEmailAsync(List<string> recipients, string subject, string htmlBody, string? senderName = null)
        {
            try
            {
                // 创建邮件消息
                var message = new EmailMessage
                {
                    To = recipients,
                    Subject = subject,
                    Body = htmlBody,
                    IsHtml = true
                };

                // 获取 Brevo 配置
                var settings = GetBrevoSettings(senderName);

                // 使用 CommercialService 发送
                var service = new CommercialService();
                return await service.SendBulkEmailAsync(message, settings);
            }
            catch (Exception ex)
            {
                // 记录错误日志
                Console.WriteLine($"邮件发送失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 发送通知邮件（预定义模板）
        /// </summary>
        public async Task<bool> SendNotificationAsync(List<string> recipients, string title, string content, Dictionary<string, string>? additionalInfo = null)
        {
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px;'>
                            📧 {title}
                        </h2>
                        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            {content}
                        </div>";

            if (additionalInfo != null && additionalInfo.Any())
            {
                htmlBody += @"
                        <h3 style='color: #34495e;'>📋 详细信息</h3>
                        <ul style='background-color: #ecf0f1; padding: 15px; border-radius: 5px;'>";

                foreach (var info in additionalInfo)
                {
                    htmlBody += $"<li><strong>{info.Key}:</strong> {info.Value}</li>";
                }

                htmlBody += "</ul>";
            }

            htmlBody += $@"
                        <hr style='margin: 30px 0; border: none; border-top: 1px solid #bdc3c7;'>
                        <p style='color: #7f8c8d; font-size: 12px; text-align: center;'>
                            此邮件由系统自动发送，发送时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                        </p>
                    </div>
                </body>
                </html>";

            return await SendEmailAsync(recipients, title, htmlBody, "系统通知");
        }

        /// <summary>
        /// 发送订单确认邮件示例
        /// </summary>
        public async Task<bool> SendOrderConfirmationAsync(string customerEmail, string orderNumber, decimal amount, List<string> items)
        {
            var itemsHtml = string.Join("", items.Select(item => $"<li>{item}</li>"));
            
            var htmlBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                        <h2 style='color: #27ae60;'>✅ 订单确认</h2>
                        <p>感谢您的订购！您的订单已确认。</p>
                        
                        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <h3>订单详情</h3>
                            <p><strong>订单号:</strong> {orderNumber}</p>
                            <p><strong>金额:</strong> ¥{amount:F2}</p>
                            <p><strong>商品清单:</strong></p>
                            <ul>{itemsHtml}</ul>
                        </div>
                        
                        <p>如有任何问题，请联系客服。</p>
                    </div>
                </body>
                </html>";

            return await SendEmailAsync(customerEmail, $"订单确认 - {orderNumber}", htmlBody, "订单系统");
        }

        /// <summary>
        /// 获取 Brevo 配置
        /// </summary>
        private EmailSettings GetBrevoSettings(string? senderName = null)
        {
            var commercialConfig = _configuration.GetSection("Email:Commercial");
            
            return new EmailSettings
            {
                SmtpServer = commercialConfig["SmtpServer"] ?? "smtp-relay.brevo.com",
                Port = int.Parse(commercialConfig["Port"] ?? "587"),
                SenderEmail = commercialConfig["SenderEmail"] ?? "yuanhw512@gmail.com",
                SenderName = senderName ?? commercialConfig["SenderName"] ?? "系统邮件",
                Username = commercialConfig["Username"] ?? throw new InvalidOperationException("Brevo SMTP Username not configured"),
                Password = commercialConfig["Password"] ?? throw new InvalidOperationException("Brevo SMTP Key not configured")
            };
        }
    }
}

// 使用示例
namespace YourProject.Examples
{
    public class EmailUsageExamples
    {
        private readonly EmailService _emailService;

        public EmailUsageExamples(EmailService emailService)
        {
            _emailService = emailService;
        }

        /// <summary>
        /// 示例1: 发送简单邮件
        /// </summary>
        public async Task Example1_SendSimpleEmail()
        {
            var result = await _emailService.SendEmailAsync(
                "user@example.com",
                "欢迎使用我们的服务",
                "<h1>欢迎！</h1><p>感谢您注册我们的服务。</p>",
                "欢迎系统"
            );

            Console.WriteLine(result ? "邮件发送成功" : "邮件发送失败");
        }

        /// <summary>
        /// 示例2: 发送批量邮件
        /// </summary>
        public async Task Example2_SendBulkEmail()
        {
            var recipients = new List<string>
            {
                "yuanyuancomecome@outlook.com",
                "rong.fan1031@gmail.com"
            };

            var result = await _emailService.SendEmailAsync(
                recipients,
                "重要通知",
                "<h2>系统维护通知</h2><p>系统将于今晚进行维护，请提前保存工作。</p>",
                "运维团队"
            );

            Console.WriteLine($"批量邮件发送{(result ? "成功" : "失败")}，共 {recipients.Count} 个收件人");
        }

        /// <summary>
        /// 示例3: 发送通知邮件
        /// </summary>
        public async Task Example3_SendNotification()
        {
            var additionalInfo = new Dictionary<string, string>
            {
                { "服务器", "Web-01" },
                { "错误代码", "500" },
                { "发生时间", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            };

            var result = await _emailService.SendNotificationAsync(
                new List<string> { "admin@company.com" },
                "系统错误警报",
                "检测到服务器异常，请及时处理。",
                additionalInfo
            );

            Console.WriteLine(result ? "警报邮件已发送" : "警报邮件发送失败");
        }

        /// <summary>
        /// 示例4: 发送订单确认邮件
        /// </summary>
        public async Task Example4_SendOrderConfirmation()
        {
            var items = new List<string>
            {
                "商品A x 2",
                "商品B x 1",
                "商品C x 3"
            };

            var result = await _emailService.SendOrderConfirmationAsync(
                "customer@example.com",
                "ORD-20260205-001",
                299.99m,
                items
            );

            Console.WriteLine(result ? "订单确认邮件已发送" : "订单确认邮件发送失败");
        }
    }
}