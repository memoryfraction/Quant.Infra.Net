using Newtonsoft.Json;
using Quant.Infra.Net.Notification.Model;
using RestSharp;
using System.Security.Cryptography;
using System.Text;
using Polly;
using Polly.Retry;

namespace Quant.Infra.Net.Notification.Service
{
    /// <summary>
    /// 钉钉通知服务，通过钉钉群机器人发送消息。
    /// DingTalk notification service that sends messages via DingTalk group robot.
    /// </summary>
    public class DingtalkService : IDingtalkService
    {
        /// <summary>
        /// 网络请求重试策略，应对暂时性失败和 API 限流。
        /// Network request retry policy to handle transient failures and API rate limiting.
        /// </summary>
        private readonly AsyncRetryPolicy _retryPolicy;

        public DingtalkService()
        {
            _retryPolicy = Policy
                .Handle<System.Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retries => System.TimeSpan.FromSeconds(System.Math.Pow(2, retries)),
                    onRetry: (response, span, retry, context) =>
                    {
                        Quant.Infra.Net.Shared.Service.UtilityService.LogAndWriteLine(
                            $"[DingTalk Retry] Attempt {{retry}}/3 after {{span.TotalSeconds}}s");
                    }
                );
        }

        /// <summary>
        /// 异步发送钉钉通知消息。
        /// Asynchronously send a DingTalk notification message.
        /// </summary>
        /// <param name="content">通知内容 / Notification content.</param>
        /// <param name="accessToken">访问令牌 / Access token for the robot.</param>
        /// <param name="secret">签名密钥 / Secret key for signing (can be null).</param>
        /// <returns>API 响应结果 / API response result.</returns>
        /// <exception cref="ArgumentException">当参数为空时抛出 / Thrown when parameters are empty.</exception>
        public async Task<RestResponse> SendNotificationAsync(string content, string accessToken, string secret)
        {
            if (string.IsNullOrWhiteSpace(content)) throw new System.ArgumentException("content must not be null or empty.", nameof(content));
            if (string.IsNullOrWhiteSpace(accessToken)) throw new System.ArgumentException("accessToken must not be null or empty.", nameof(accessToken));

            var timestamp = ((System.DateTime.UtcNow.Ticks - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc).Ticks) / 10000).ToString();
            var stringToSign = timestamp + "\n" + (secret ?? "");
            var sign = EncryptWithSHA256(stringToSign, secret);

            string url;
            if (!string.IsNullOrEmpty(secret))
            {
                url = $"https://oapi.dingtalk.com/robot/send?access_token={{accessToken}}&timestamp={{timestamp}}&sign={{sign}}";
            }
            else
            {
                url = $"https://oapi.dingtalk.com/robot/send?access_token={{accessToken}}";
            }

            var targetURL = new System.Uri(url);
            var client = new RestClient(targetURL);
            var request = new RestRequest(targetURL, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            var messageBody = new MessageBody()
            {
                text = new text()
                {
                    content = content
                }
            };
            var body = JsonConvert.SerializeObject(messageBody);
            request.AddParameter("application/json", body, ParameterType.RequestBody);

            return await _retryPolicy.ExecuteAsync(async () => await client.ExecuteAsync(request));
        }

        /// <summary>
        /// 使用 HMAC-SHA256 加密并返回 URL-encoded Base64 结果。
        /// Encrypt with HMAC-SHA256 and return URL-encoded Base64 result.
        /// </summary>
        /// <param name="data">待加密数据 / Data to encrypt.</param>
        /// <param name="secret">密钥 / Secret key.</param>
        /// <returns>Base64 编码的签名 / Base64-encoded signature.</returns>
        private static string EncryptWithSHA256(string data, string secret)
        {
            secret = secret ?? "";
            var encoding = Encoding.UTF8;
            byte[] keyByte = encoding.GetBytes(secret);
            byte[] dataBytes = encoding.GetBytes(data);

            using (var hmac256 = new HMACSHA256(keyByte))
            {
                byte[] hashData = hmac256.ComputeHash(dataBytes);
                var base64Str = Convert.ToBase64String(hashData);
                return System.Web.HttpUtility.UrlEncode(base64Str, Encoding.UTF8);
            }
        }
    }
}
