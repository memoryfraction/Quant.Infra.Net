using System;
using System.Threading.Tasks;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 刷新令牌仓储接口。
    /// Refresh token repository interface.
    /// </summary>
    public interface IRefreshTokenRepository
    {
        /// <summary>
        /// 根据 token hash 获取刷新令牌记录。
        /// Get refresh token record by its hash.
        /// </summary>
        Task<RefreshTokenRecord?> GetByHashAsync(string tokenHash);

        /// <summary>
        /// 添加刷新令牌记录。
        /// Add a refresh token record.
        /// </summary>
        Task AddAsync(RefreshTokenRecord record);

        /// <summary>
        /// 标记刷新令牌为撤销。
        /// Revoke a refresh token by hash.
        /// </summary>
        Task RevokeAsync(string tokenHash);
    }
}
