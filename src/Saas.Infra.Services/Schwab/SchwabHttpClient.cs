using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Saas.Infra.Core.Schwab;
using Serilog;

namespace Saas.Infra.Services.Schwab
{
    /// <summary>
    /// 嘉信理财 HTTP 客户端辅助类。
    /// Charles Schwab HTTP client helper.
    /// </summary>
    public class SchwabHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly SchwabOptions _options;
        private readonly ISchwabTokenRepository _tokenRepository;

        /// <summary>
        /// 初始化 <see cref="SchwabHttpClient"/> 的新实例。
        /// Initializes a new instance of the <see cref="SchwabHttpClient"/> class.
        /// </summary>
        /// <param name="httpClient">HTTP 客户端。 / HTTP client.</param>
        /// <param name="options">配置选项。 / Configuration options.</param>
        /// <param name="tokenRepository">令牌仓储。 / Token repository.</param>
        public SchwabHttpClient(
            HttpClient httpClient,
            IOptions<SchwabOptions> options,
            ISchwabTokenRepository tokenRepository)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));

            _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
        }

        /// <summary>
        /// 发送 GET 请求。
        /// Sends GET request.
        /// </summary>
        /// <typeparam name="T">响应类型。 / Response type.</typeparam>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="path">请求路径。 / Request path.</param>
        /// <returns>响应对象。 / Response object.</returns>
        public async Task<T> GetAsync<T>(Guid userId, string path)
        {
            var accessToken = await GetValidAccessTokenAsync(userId);
            
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }

        /// <summary>
        /// 发送 POST 请求。
        /// Sends POST request.
        /// </summary>
        /// <typeparam name="T">响应类型。 / Response type.</typeparam>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="path">请求路径。 / Request path.</param>
        /// <param name="body">请求体。 / Request body.</param>
        /// <returns>响应对象。 / Response object.</returns>
        public async Task<T> PostAsync<T>(Guid userId, string path, object body)
        {
            var accessToken = await GetValidAccessTokenAsync(userId);
            
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }

        /// <summary>
        /// 发送 DELETE 请求。
        /// Sends DELETE request.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="path">请求路径。 / Request path.</param>
        /// <returns>是否成功。 / Whether successful.</returns>
        public async Task<bool> DeleteAsync(Guid userId, string path)
        {
            var accessToken = await GetValidAccessTokenAsync(userId);
            
            var request = new HttpRequestMessage(HttpMethod.Delete, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// 获取有效的访问令牌（如果过期则自动刷新）。
        /// Gets valid access token (auto-refresh if expired).
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>访问令牌。 / Access token.</returns>
        private async Task<string> GetValidAccessTokenAsync(Guid userId)
        {
            var token = await _tokenRepository.GetByUserIdAsync(userId);
            
            if (token == null)
            {
                throw new InvalidOperationException("User is not authorized with Schwab. Please authorize first.");
            }

            // 如果令牌即将过期（提前1分钟），刷新它
            if (token.IsAccessTokenExpired)
            {
                Log.Information("Access token expired for user {UserId}, refreshing...", userId);
                // TODO: 调用 SchwabAuthService.RefreshAccessTokenAsync
                // 这里暂时抛出异常，等实现 AuthService 后再完善
                throw new InvalidOperationException("Access token expired. Please re-authorize.");
            }

            return token.AccessToken;
        }
    }
}
