namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 通知路由配置（§5.6 通知网关）。
/// Notification routing configuration (design §5.6).
/// </summary>
public class NotificationOptions
{
    /// <summary>
    /// 是否启用通知（false → 所有级别静默跳过）。
    /// Master switch (false → all severities silently skipped).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 钉钉 access token（信息 / 警告级别）。
    /// DingTalk access token (Info / Warning).
    /// </summary>
    public string? DingtalkAccessToken { get; set; }

    /// <summary>
    /// 钉钉 secret（签名密钥）。
    /// DingTalk secret (signing secret).
    /// </summary>
    public string? DingtalkSecret { get; set; }

    /// <summary>
    /// 企业微信 WebHook URL（警告 / 严重级别）。
    /// WeChat Work webhook URL (Warning / Critical).
    /// </summary>
    public string? WeChatWebHook { get; set; }

    /// <summary>
    /// 邮件收件人列表（严重级别）。
    /// Email recipients (Critical).
    /// </summary>
    public string[] EmailRecipients { get; set; } = Array.Empty<string>();

    /// <summary>
    /// SMTP 服务器地址。
    /// SMTP server host.
    /// </summary>
    public string? EmailSmtpServer { get; set; }

    /// <summary>
    /// SMTP 端口。
    /// SMTP port.
    /// </summary>
    public int EmailPort { get; set; } = 587;

    /// <summary>
    /// 发件人地址。
    /// Sender address.
    /// </summary>
    public string? EmailSender { get; set; }

    /// <summary>
    /// SMTP 用户名。
    /// SMTP username.
    /// </summary>
    public string? EmailUsername { get; set; }

    /// <summary>
    /// SMTP 密码。
    /// SMTP password.
    /// </summary>
    public string? EmailPassword { get; set; }
}
