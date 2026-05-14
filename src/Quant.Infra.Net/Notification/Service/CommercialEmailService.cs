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
                UtilityService.LogAndWriteLine($"[CommercialService - Brevo] Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendRealBrevoEmail(EmailMessage message, EmailSettingBase setting)
        {
            UtilityService.LogAndWriteLine($"[CommercialService - Brevo] Starting real email delivery");
            UtilityService.LogAndWriteLine($"[Brevo] Sender: {setting.SenderEmail} ({setting.SenderName})");
            UtilityService.LogAndWriteLine($"[Brevo] Subject: {message.Subject}");
            UtilityService.LogAndWriteLine($"[Brevo] Recipient count: {message.To.Count}");
            UtilityService.LogAndWriteLine($"[Brevo] SMTP server: {setting.SmtpServer}:{setting.Port}");

            using var client = new MailKit.Net.Smtp.SmtpClient();

            try
            {
                UtilityService.LogAndWriteLine($"[Brevo] Connecting to SMTP server...");
                // 连接到 Brevo SMTP 服务器
                await client.ConnectAsync(setting.SmtpServer, setting.Port, MailKit.Security.SecureSocketOptions.StartTls);
                UtilityService.LogAndWriteLine($"[Brevo] Connected to SMTP server");

                UtilityService.LogAndWriteLine($"[Brevo] Authenticating...");
                
                // 检查密钥类型
                if (setting.Password.StartsWith("xkeysib-"))
                {
                    UtilityService.LogAndWriteLine($"[Brevo] API key detected, but SMTP requires SMTP credentials");
                    UtilityService.LogAndWriteLine($"[Brevo] Get SMTP credentials with these steps:");
                    UtilityService.LogAndWriteLine($"[Brevo] 1. Sign in to your Brevo account");
                    UtilityService.LogAndWriteLine($"[Brevo] 2. Open Settings -> SMTP & API from the top-right profile menu");
                    UtilityService.LogAndWriteLine($"[Brevo] 3. Generate a new SMTP key from the SMTP tab");
                    UtilityService.LogAndWriteLine($"[Brevo] 4. Copy the SMTP username and SMTP key");
                    UtilityService.LogAndWriteLine($"[Brevo] 5. Update user secrets:");
                    UtilityService.LogAndWriteLine($"[Brevo]    dotnet user-secrets set \"Email:Commercial:Username\" \"your-smtp-username\"");
                    UtilityService.LogAndWriteLine($"[Brevo]    dotnet user-secrets set \"Email:Commercial:Password\" \"your-smtp-key\"");
                    return false;
                }
                else if (!setting.Password.StartsWith("xsmtpsib-"))
                {
                    UtilityService.LogAndWriteLine($"[Brevo] Unrecognized key format. Expected SMTP key (xsmtpsib-...)");
                    return false;
                }
                
                // 检查是否有 SMTP 用户名
                if (string.IsNullOrEmpty(setting.Username))
                {
                    UtilityService.LogAndWriteLine($"[Brevo] Missing SMTP username");
                    UtilityService.LogAndWriteLine($"[Brevo] Get the SMTP username from Brevo SMTP settings, then run:");
                    UtilityService.LogAndWriteLine($"[Brevo] dotnet user-secrets set \"Email:Commercial:Username\" \"your-smtp-username\"");
                    return false;
                }
                
                // 使用 SMTP 用户名和密钥进行认证
                string smtpUsername = !string.IsNullOrEmpty(setting.Username) ? setting.Username : setting.SenderEmail;
                await client.AuthenticateAsync(smtpUsername, setting.Password);
                UtilityService.LogAndWriteLine($"[Brevo] Authentication succeeded. Username: {smtpUsername}");

                foreach (var recipient in message.To)
                {
                    UtilityService.LogAndWriteLine($"[Brevo] Preparing email for: {recipient}");
                    
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

                    UtilityService.LogAndWriteLine($"[Brevo] Sending email to: {recipient}");
                    // 发送邮件
                    await client.SendAsync(email);
                    UtilityService.LogAndWriteLine($"[Brevo] Real email sent to: {recipient}");

                    // 批量发送时添加延迟，避免触发限制
                    if (message.To.Count > 1)
                    {
                        await Task.Delay(1000); // 1秒延迟
                    }
                }

                UtilityService.LogAndWriteLine($"[Brevo] Disconnecting...");
                await client.DisconnectAsync(true);
                UtilityService.LogAndWriteLine($"[Brevo] Real email delivery finished. Sent {message.To.Count} messages");
                return true;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Brevo SMTP] Send failed: {ex.Message}");
                UtilityService.LogAndWriteLine($"[Brevo SMTP] Error details: {ex}");
                
                // 如果是认证错误，提供更详细的信息
                if (ex.Message.Contains("authentication") || ex.Message.Contains("Authentication"))
                {
                    UtilityService.LogAndWriteLine($"[Brevo SMTP] Authentication failed. Please check:");
                    UtilityService.LogAndWriteLine($"[Brevo SMTP] 1. Username should be the sender email: {setting.SenderEmail}");
                    UtilityService.LogAndWriteLine($"[Brevo SMTP] 2. Password should be the Brevo API key");
                    UtilityService.LogAndWriteLine($"[Brevo SMTP] 3. Sender email must be verified in Brevo");
                }
                
                return false;
            }
        }

        private async Task<bool> SimulateBrevoEmail(EmailMessage message, EmailSettingBase setting)
        {
            UtilityService.LogAndWriteLine($"[CommercialService - Brevo] Starting simulated bulk email delivery");
            UtilityService.LogAndWriteLine($"[Brevo] Sender: {setting.SenderEmail} ({setting.SenderName})");
            UtilityService.LogAndWriteLine($"[Brevo] Subject: {message.Subject}");
            UtilityService.LogAndWriteLine($"[Brevo] Recipient count: {message.To.Count}");
            UtilityService.LogAndWriteLine($"[Brevo] Message format: {(message.IsHtml ? "HTML" : "plain text")}");

            // 模拟 Brevo API 调用延迟
            await Task.Delay(500);

            // 模拟批量发送处理
            foreach (var recipient in message.To)
            {
                UtilityService.LogAndWriteLine($"[Brevo] Sending to: {recipient}");
                
                // 模拟单个邮件发送延迟
                await Task.Delay(100);
                
                UtilityService.LogAndWriteLine($"[Brevo] Sent to: {recipient}");
            }

            UtilityService.LogAndWriteLine($"[Brevo] Bulk email delivery finished. Sent {message.To.Count} messages");
            UtilityService.LogAndWriteLine($"[Brevo] Message body preview: {(message.Body.Length > 50 ? message.Body.Substring(0, 50) + "..." : message.Body)}");

            return true;
        }
    }
}


