using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Shared.Service;
using System;
using System.Threading.Tasks;
using MimeKit;


namespace Quant.Infra.Net.Notification.Service
{
    public class CommercialEmailService : IEmailService
    {
		private readonly Microsoft.Extensions.Hosting.IHostEnvironment _env;
        public CommercialEmailService(Microsoft.Extensions.Hosting.IHostEnvironment env)
        {
            _env = env;
		}
		public async Task<bool> SendBulkEmailAsync(EmailMessage message, EmailSettingBase setting)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (message == null) throw new ArgumentNullException(nameof(message));

            try
            {
				// todo 如果是生产环境，打印 Password， 帮助判断取值是否正确
				var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
				if (envName.Equals("Production", StringComparison.OrdinalIgnoreCase))
                {
					// UtilityService.LogAndWriteLine($"Password: {setting.Password ?? ""}"); // debug purpose
				}
				return await SendRealBrevoEmail(message, setting);

            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[CommercialService - Brevo] 错误: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendRealBrevoEmail(EmailMessage message, EmailSettingBase setting)
        {
            UtilityService.LogAndWriteLine($"[CommercialService - Brevo] 开始真实邮件发送");
            UtilityService.LogAndWriteLine($"[Brevo] 发件人: {setting.SenderEmail} ({setting.SenderName})");
            UtilityService.LogAndWriteLine($"[Brevo] 主题: {message.Subject}");
            UtilityService.LogAndWriteLine($"[Brevo] 收件人数量: {message.To.Count}");
            UtilityService.LogAndWriteLine($"[Brevo] SMTP 服务器: {setting.SmtpServer}:{setting.Port}");

            using var client = new MailKit.Net.Smtp.SmtpClient();

            try
            {
                UtilityService.LogAndWriteLine($"[Brevo] 正在连接到 SMTP 服务器...");
                // 连接到 Brevo SMTP 服务器
                await client.ConnectAsync(setting.SmtpServer, setting.Port, MailKit.Security.SecureSocketOptions.StartTls);
                UtilityService.LogAndWriteLine($"[Brevo] ✓ 已连接到 SMTP 服务器");

                UtilityService.LogAndWriteLine($"[Brevo] 正在进行身份验证...");
                
                // 检查密钥类型
                if (setting.Password.StartsWith("xkeysib-"))
                {
                    UtilityService.LogAndWriteLine($"[Brevo] ❌ 检测到 API Key，但 SMTP 需要 SMTP 凭据");
                    UtilityService.LogAndWriteLine($"[Brevo] 请按以下步骤获取 SMTP 凭据：");
                    UtilityService.LogAndWriteLine($"[Brevo] 1. 登录 Brevo 账户");
                    UtilityService.LogAndWriteLine($"[Brevo] 2. 点击右上角头像 → Settings → SMTP & API");
                    UtilityService.LogAndWriteLine($"[Brevo] 3. 在 SMTP 标签页中，生成新的 SMTP 密钥");
                    UtilityService.LogAndWriteLine($"[Brevo] 4. 复制 SMTP 用户名和 SMTP 密钥");
                    UtilityService.LogAndWriteLine($"[Brevo] 5. 更新用户机密：");
                    UtilityService.LogAndWriteLine($"[Brevo]    dotnet user-secrets set \"Email:Commercial:Username\" \"你的SMTP用户名\"");
                    UtilityService.LogAndWriteLine($"[Brevo]    dotnet user-secrets set \"Email:Commercial:Password\" \"你的SMTP密钥\"");
                    return false;
                }
                else if (!setting.Password.StartsWith("xsmtpsib-"))
                {
                    UtilityService.LogAndWriteLine($"[Brevo] ❌ 未识别的密钥格式，期望 SMTP 密钥 (xsmtpsib-...)");
                    return false;
                }
                
                // 检查是否有 SMTP 用户名
                if (string.IsNullOrEmpty(setting.Username))
                {
                    UtilityService.LogAndWriteLine($"[Brevo] ❌ 缺少 SMTP 用户名");
                    UtilityService.LogAndWriteLine($"[Brevo] 请在 Brevo SMTP 设置页面获取 SMTP 用户名，然后运行：");
                    UtilityService.LogAndWriteLine($"[Brevo] dotnet user-secrets set \"Email:Commercial:Username\" \"你的SMTP用户名\"");
                    return false;
                }
                
                // 使用 SMTP 用户名和密钥进行认证
                string smtpUsername = !string.IsNullOrEmpty(setting.Username) ? setting.Username : setting.SenderEmail;
                await client.AuthenticateAsync(smtpUsername, setting.Password);
                UtilityService.LogAndWriteLine($"[Brevo] ✓ 身份验证成功，用户名: {smtpUsername}");

                foreach (var recipient in message.To)
                {
                    UtilityService.LogAndWriteLine($"[Brevo] 正在准备邮件给: {recipient}");
                    
                    var email = new MimeMessage();
                    email.From.Add(new MailboxAddress(setting.SenderName ?? string.Empty, setting.SenderEmail));
                    email.To.Add(MailboxAddress.Parse(recipient));
                    email.Subject = message.Subject;

                    var bodyBuilder = new BodyBuilder
                    {
                        HtmlBody = message.IsHtml ? message.Body : null,
                        TextBody = !message.IsHtml ? message.Body : null
                    };
                    email.Body = bodyBuilder.ToMessageBody();

                    UtilityService.LogAndWriteLine($"[Brevo] 正在发送邮件至: {recipient}");
                    // 发送邮件
                    await client.SendAsync(email);
                    UtilityService.LogAndWriteLine($"[Brevo] ✓ 真实邮件已发送至: {recipient}");

                    // 批量发送时添加延迟，避免触发限制
                    if (message.To.Count > 1)
                    {
                        await Task.Delay(1000); // 1秒延迟
                    }
                }

                UtilityService.LogAndWriteLine($"[Brevo] 正在断开连接...");
                await client.DisconnectAsync(true);
                UtilityService.LogAndWriteLine($"[Brevo] 真实邮件发送完成，共发送 {message.To.Count} 封邮件");
                return true;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Brevo SMTP] 发送失败: {ex.Message}");
                UtilityService.LogAndWriteLine($"[Brevo SMTP] 错误详情: {ex}");
                
                // 如果是认证错误，提供更详细的信息
                if (ex.Message.Contains("authentication") || ex.Message.Contains("Authentication"))
                {
                    UtilityService.LogAndWriteLine($"[Brevo SMTP] 认证失败 - 请检查:");
                    UtilityService.LogAndWriteLine($"[Brevo SMTP] 1. 用户名应该是发件人邮箱: {setting.SenderEmail}");
                    UtilityService.LogAndWriteLine($"[Brevo SMTP] 2. 密码应该是 Brevo API Key");
                    UtilityService.LogAndWriteLine($"[Brevo SMTP] 3. 发件人邮箱需要在 Brevo 中验证");
                }
                
                return false;
            }
        }

        private async Task<bool> SimulateBrevoEmail(EmailMessage message, EmailSettingBase setting)
        {
            UtilityService.LogAndWriteLine($"[CommercialService - Brevo] 开始模拟批量邮件发送");
            UtilityService.LogAndWriteLine($"[Brevo] 发件人: {setting.SenderEmail} ({setting.SenderName})");
            UtilityService.LogAndWriteLine($"[Brevo] 主题: {message.Subject}");
            UtilityService.LogAndWriteLine($"[Brevo] 收件人数量: {message.To.Count}");
            UtilityService.LogAndWriteLine($"[Brevo] 邮件格式: {(message.IsHtml ? "HTML" : "纯文本")}");

            // 模拟 Brevo API 调用延迟
            await Task.Delay(500);

            // 模拟批量发送处理
            foreach (var recipient in message.To)
            {
                UtilityService.LogAndWriteLine($"[Brevo] 正在发送至: {recipient}");
                
                // 模拟单个邮件发送延迟
                await Task.Delay(100);
                
                UtilityService.LogAndWriteLine($"[Brevo] ✓ 成功发送至: {recipient}");
            }

            UtilityService.LogAndWriteLine($"[Brevo] 批量邮件发送完成，共发送 {message.To.Count} 封邮件");
            UtilityService.LogAndWriteLine($"[Brevo] 邮件内容预览: {(message.Body.Length > 50 ? message.Body.Substring(0, 50) + "..." : message.Body)}");

            return true;
        }
    }
}
