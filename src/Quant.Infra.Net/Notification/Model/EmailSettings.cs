namespace Quant.Infra.Net.Notification.Model
{
	public class EmailSettings
	{
		public string SmtpServer { get; set; } = string.Empty;
		public int Port { get; set; }
		public string SenderEmail { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty; // 126邮箱此处填写“授权码”
		public string SenderName { get; set; }
	}
}
