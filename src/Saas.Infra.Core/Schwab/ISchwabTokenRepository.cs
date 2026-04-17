using System;
using System.Threading.Tasks;

namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财令牌仓储接口。
    /// Charles Schwab token repository interface.
    /// </summary>
    public interface ISchwabTokenRepository
    {
        /// <summary>
        /// 根据用户 ID 获取令牌。
        /// Gets token by user ID.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>令牌响应，如果不存在则返回 null。 / Token response, or null if not found.</returns>
        Task<SchwabTokenResponse?> GetByUserIdAsync(Guid userId);

        /// <summary>
        /// 保存或更新令牌。
        /// Saves or updates token.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="tokenResponse">令牌响应。 / Token response.</param>
        /// <returns>异步任务。 / Async task.</returns>
        Task SaveOrUpdateAsync(Guid userId, SchwabTokenResponse tokenResponse);

        /// <summary>
        /// 删除用户的令牌。
        /// Deletes user's token.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>异步任务。 / Async task.</returns>
        Task DeleteAsync(Guid userId);

        /// <summary>
        /// 检查用户是否已授权。
        /// Checks if user is authorized.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>是否已授权。 / Whether authorized.</returns>
        Task<bool> IsAuthorizedAsync(Guid userId);
    }
}
