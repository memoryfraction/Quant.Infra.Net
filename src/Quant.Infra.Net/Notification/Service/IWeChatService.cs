using RestSharp;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
    /// <summary>
    /// 企业微信通知服务接口，通过 Webhook 发送文本消息。
    /// WeChat Work notification service interface for sending text messages via webhook.
    /// </summary>
    public interface IWeChatService
    {
        /// <summary>
        /// 异步发送文本通知到企业微信群。
        /// Asynchronously send a text notification to a WeChat Work group via webhook.
        /// </summary>
        /// <param name="content">消息内容 / Message content.</param>
        /// <param name="webHook">Webhook URL / Webhook URL for the target group.</param>
        /// <returns>HTTP 响应结果 / HTTP response result from WeChat API.</returns>
        Task<RestResponse> SendTextNotificationAsync(string content, string webHook);
    }
}
