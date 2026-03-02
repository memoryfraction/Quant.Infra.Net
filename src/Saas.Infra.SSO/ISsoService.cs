using Saas.Infra.Core;
using System.Security.Claims;

namespace Saas.Infra.SSO
{
    /// <summary>
    /// 定义单点登录(SSO)服务的契约。
    /// 提供JWT令牌生成、验证和撤销功能。
    /// </summary>
    public interface ISsoService
	{
		/// <summary>
		/// 异步生成JWT访问令牌和刷新令牌。
		/// </summary>
		/// <param name="userId">用户唯一标识符，不能为null或空字符串。</param>
		/// <param name="clientId">客户端唯一标识符，不能为null或空字符串。</param>
		/// <returns>
		/// 包含JWT令牌信息的 <see cref="JwtTokenResponse"/> 对象。
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 当 <paramref name="userId"/> 或 <paramref name="clientId"/> 为null时抛出。
		/// </exception>
		/// <exception cref="ArgumentException">
		/// 当 <paramref name="userId"/> 或 <paramref name="clientId"/> 为空字符串时抛出。
		/// </exception>
		Task<JwtTokenResponse> GenerateTokensAsync(string userId, string clientId);

		/// <summary>
		/// 异步验证JWT令牌并提取声明主体。
		/// </summary>
		/// <param name="token">要验证的JWT令牌字符串。</param>
		/// <returns>
		/// 如果令牌有效，返回包含用户声明的 <see cref="ClaimsPrincipal"/> 对象；
		/// 如果令牌无效，返回null。
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 当 <paramref name="token"/> 为null时抛出。
		/// </exception>
		/// <exception cref="ArgumentException">
		/// 当 <paramref name="token"/> 为空字符串时抛出。
		/// </exception>
		Task<ClaimsPrincipal> ValidateTokenAsync(string token);

		/// <summary>
		/// 异步撤销指定的刷新令牌，使其失效。
		/// </summary>
		/// <param name="refreshToken">要撤销的刷新令牌字符串。</param>
		/// <returns>表示异步操作的任务。</returns>
		/// <exception cref="ArgumentNullException">
		/// 当 <paramref name="refreshToken"/> 为null时抛出。
		/// </exception>
		/// <exception cref="ArgumentException">
		/// 当 <paramref name="refreshToken"/> 为空字符串时抛出。
		/// </exception>
		/// <exception cref="InvalidTokenException">
		/// 当刷新令牌无效或不存在时抛出。
		/// </exception>
		Task RevokeRefreshTokenAsync(string refreshToken);
	}
}
