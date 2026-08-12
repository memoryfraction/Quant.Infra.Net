using Quant.Infra.Net.Notification.Model;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
    /// <summary>
    /// 电子邮件服务接口，用于发送邮件通知。
    /// Interface for sending email notifications.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// 异步批量发送邮件。
        /// Asynchronously send an email to one or more recipients.
        /// </summary>
        /// <param name="message">邮件消息体 / Email message payload.</param>
        /// <param name="setting">SMTP/Brevo 配置 / SMTP or Brevo configuration.</param>
        /// <returns>发送成功返回 true / Returns true if sent successfully.</returns>
        Task<bool> SendBulkEmailAsync(EmailMessage message, EmailSettingBase setting);
    }
}
