using RestSharp;
using System;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;

namespace Quant.Infra.Net.Notification.Service
{
    /// <summary>
    /// 企业微信群机器人通知服务。
    /// WeChat Work group robot notification service.
    /// </summary>
    public class WeChatService : IWeChatService
    {
        /// <summary>
        /// 网络请求重试策略，应对暂时性失败和 API 限流。
        /// Network request retry policy to handle transient failures and API rate limiting.
        /// </summary>
        private readonly AsyncRetryPolicy _retryPolicy;

        public WeChatService()
        {
            _retryPolicy = Policy
                .Handle<System.Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retries => System.TimeSpan.FromSeconds(System.Math.Pow(2, retries)),
                    onRetry: (response, span, retry, context) =>
                    {
                        Quant.Infra.Net.Shared.Service.UtilityService.LogAndWriteLine(
                            $"[WeChat Retry] Attempt {{retry}}/3 after {{span.TotalSeconds}}s");
                    }
                );
        }

        /// <summary>
        /// 通过企业微信群机器人发送文本通知。
        /// Send a text notification via WeChat Work group robot.
        /// </summary>
        /// <param name="content">通知内容 / Notification content.</param>
        /// <param name="webHook">Webhook URL / Webhook URL for the robot.</param>
        /// <returns>API 响应结果 / API response result.</returns>
        /// <exception cref="ArgumentException">当参数为空时抛出 / Thrown when parameters are empty.</exception>
        public async Task<RestResponse> SendTextNotificationAsync(string content, string webHook)
        {
            if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("content must not be null or empty.", nameof(content));
            if (string.IsNullOrWhiteSpace(webHook)) throw new ArgumentException("webHook must not be null or empty.", nameof(webHook));

            var client = new RestClient();
            var request = new RestRequest(webHook, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddJsonBody(new { msgtype = "text", text = new { content = content } });

            return await _retryPolicy.ExecuteAsync(async () => await client.ExecuteAsync(request));
        }
    }
}
