namespace Quant.Infra.Net.Notification.Model
{
	/// <summary>
	/// 邮件配置基类，封装通用 SMTP 属性
	/// </summary>
	public abstract class EmailSettingBase
	{
		public string SmtpServer { get; set; } = string.Empty;
		public int Port { get; set; }
		public string SenderEmail { get; set; } = string.Empty;
		public string SenderName { get; set; } = string.Empty;
		public string Username { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
	}

	/// <summary>
	/// 个人邮件设置（继承基类）
	/// </summary>
	public class PersonalEmailSetting : EmailSettingBase
	{
		// 目前无需额外属性，但保留类以便未来扩展或用于 DI 区分
	}

	/// <summary>
	/// 商业邮件设置（继承基类）
	/// </summary>
	public class CommercialEmailSetting : EmailSettingBase
	{
		// 示例：未来可能添加商业专有的 API 密钥
		// public string ApiKey { get; set; } 
	}

	public class EmailSettings
	{
		public string Type { get; set; } = string.Empty; // "Personal" 或 "Commercial"
		public PersonalEmailSetting Personal { get; set; } = new PersonalEmailSetting();
		public CommercialEmailSetting Commercial { get; set; } = new CommercialEmailSetting();
	}
}