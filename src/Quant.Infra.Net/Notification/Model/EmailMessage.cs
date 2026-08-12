using System.Collections.Generic;

namespace Quant.Infra.Net.Notification.Model
{
    /// <summary>
    /// 电子邮件消息模型，包含收件人、主题和正文。
    /// Email message model containing recipients, subject, and body.
    /// </summary>
    public class EmailMessage
    {
        /// <summary>
        /// 收件人地址列表 / List of recipient email addresses.
        /// </summary>
        public List<string> To { get; set; } = new();

        /// <summary>
        /// 邮件主题 / Email subject line.
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// 邮件正文（可以是纯文本或 HTML）/ Email body (plain text or HTML).
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// 是否为 HTML 格式 / Whether the body is in HTML format.
        /// </summary>
        public bool IsHtml { get; set; } = true;
    }
}
