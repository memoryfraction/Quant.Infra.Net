using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财账户仓储接口。
    /// Charles Schwab account repository interface.
    /// </summary>
    public interface ISchwabAccountRepository
    {
        /// <summary>
        /// 根据用户 ID 获取所有账户。
        /// Gets all accounts by user ID.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>账户列表。 / List of accounts.</returns>
        Task<IReadOnlyList<SchwabAccountNumber>> GetByUserIdAsync(Guid userId);

        /// <summary>
        /// 根据账户哈希值获取账户。
        /// Gets account by hash value.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="hashValue">账户哈希值。 / Account hash value.</param>
        /// <returns>账户信息，如果不存在则返回 null。 / Account info, or null if not found.</returns>
        Task<SchwabAccountNumber?> GetByHashValueAsync(Guid userId, string hashValue);

        /// <summary>
        /// 保存或更新账户列表。
        /// Saves or updates account list.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="accounts">账户列表。 / List of accounts.</param>
        /// <returns>异步任务。 / Async task.</returns>
        Task SaveOrUpdateAsync(Guid userId, IEnumerable<SchwabAccountNumber> accounts);

        /// <summary>
        /// 删除用户的所有账户。
        /// Deletes all accounts for user.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>异步任务。 / Async task.</returns>
        Task DeleteByUserIdAsync(Guid userId);

        /// <summary>
        /// 设置主账户。
        /// Sets primary account.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="hashValue">账户哈希值。 / Account hash value.</param>
        /// <returns>异步任务。 / Async task.</returns>
        Task SetPrimaryAccountAsync(Guid userId, string hashValue);

        /// <summary>
        /// 获取主账户。
        /// Gets primary account.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>主账户信息，如果不存在则返回 null。 / Primary account info, or null if not found.</returns>
        Task<SchwabAccountNumber?> GetPrimaryAccountAsync(Guid userId);
    }
}
