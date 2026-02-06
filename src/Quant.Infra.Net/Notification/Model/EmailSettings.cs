namespace Quant.Infra.Net.Notification.Model
{
	public class EmailSettings
	{
		public string SmtpServer { get; set; } = string.Empty;
		public int Port { get; set; }
		public string SenderEmail { get; set; } = string.Empty;
		public string Username { get; set; } = string.Empty; // SMTP 用户名（对于 Brevo 是专门的 SMTP 用户名）
		public string Password { get; set; } = string.Empty; // 126邮箱此处填写"授权码"，Brevo 填写 SMTP 密钥
		public string SenderName { get; set; }
	}
}