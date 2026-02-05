using Quant.Infra.Net.Notification.Model;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Quant.Infra.Net.Notification.Service
{
    /// <summary>
    /// 商业邮件服务 - 使用Brevo API进行大批量邮件发送
    /// </summary>
    public class CommercialService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public CommercialService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<bool> SendBulkEmailAsync(EmailMessage message, EmailSettings setting)
        {
            try
            {
                // 从配置中获取Brevo设置
                var brevoConfig = _configuration.GetSection("BrevoEmail");
                var apiKey = brevoConfig["ApiKey"];
                var fromEmail = brevoConfig["FromEmail"];
                var fromName = brevoConfig["FromName"] ?? "BTC技术分析报告";
                var apiUrl = brevoConfig["ApiUrl"] ?? "https://api.brevo.com/v3/smtp/email";

                if (string.IsNullOrEmpty(apiKey))
                {
                    throw new InvalidOperationException("Brevo API key is not configured.");
                }

                var payload = new
                {
                    sender = new { name = fromName, email = fromEmail },
                    to = message.To.Select(email => new { email }).ToArray(),
                    subject = message.Subject,
                    htmlContent = message.IsHtml ? message.Body : null,
                    textContent = !message.IsHtml ? message.Body : null
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);

                var response = await _httpClient.PostAsync(apiUrl, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
