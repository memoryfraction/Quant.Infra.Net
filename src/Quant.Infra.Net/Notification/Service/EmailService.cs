using System;
using System.Threading.Tasks;
using Quant.Infra.Net.Notification.Model;
using MimeKit;
using Quant.Infra.Net.Shared.Service;

namespace Quant.Infra.Net.Notification.Service
{
	/// <summary>
	/// 个人邮件发送服务实现。
	/// Personal email sending service implementation.
	/// </summary>
	public class PersonalEmailService : IEmailService
	{
		/// <summary>
		/// 异步批量发送邮件。
		/// Sends bulk emails asynchronously.
		/// </summary>
		/// <param name="message">邮件消息 / The email message.</param>
		/// <param name="setting">邮件配置 / The email settings.</param>
		/// <returns>是否发送成功 / Whether the sending was successful.</returns>
		/// <exception cref="ArgumentNullException">当 setting 为 null 时抛出 / Thrown when setting is null.</exception>
		public async Task<bool> SendBulkEmailAsync(EmailMessage message, EmailSettingBase setting)
		{
			if (setting == null) throw new ArgumentNullException(nameof(setting));

			// 明确指定使用 MailKit 的 SmtpClient，防止和 System.Net.Mail 冲突
			using var client = new MailKit.Net.Smtp.SmtpClient();

			try
			{
				// 解决 465 端口报错：第三个参数设为 true
				bool useSsl = setting.Port == 465;
				await client.ConnectAsync(setting.SmtpServer, setting.Port, useSsl);

				// 身份验证
				await client.AuthenticateAsync(setting.SenderEmail, setting.Password);

				foreach (var recipient in message.To)
				{
					var email = new MimeMessage();
					email.From.Add(new MailboxAddress(setting.SenderName ?? "Quant System", setting.SenderEmail));
					email.To.Add(MailboxAddress.Parse(recipient));
					email.Subject = message.Subject;

					var bodyBuilder = new BodyBuilder
					{
						HtmlBody = message.IsHtml ? message.Body : null,
						TextBody = !message.IsHtml ? message.Body : null
					};
					email.Body = bodyBuilder.ToMessageBody();

					// 发送邮件
					await client.SendAsync(email);

					if (message.To.Count > 1)
					{
						await Task.Delay(2000);
					}
				}

				await client.DisconnectAsync(true);
				return true;
			}
			catch (Exception ex)
			{
				UtilityService.LogAndWriteLine($"[PersonalEmailService] Error: {ex.Message}");
				return false;
			}
		}
	}
}