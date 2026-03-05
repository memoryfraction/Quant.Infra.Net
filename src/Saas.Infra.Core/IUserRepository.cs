using System.Threading.Tasks;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 用户仓储接口，负责从数据存储中读取用户信息。
    /// User repository interface for reading user information from the data store.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// 根据用户名异步获取用户实体，如果不存在则返回 null。
        /// Gets a user entity by username asynchronously; returns null if not found.
        /// </summary>
        Task<User?> GetByUsernameAsync(string username);

        /// <summary>
        /// 根据电子邮件地址异步获取用户实体，如果不存在则返回 null。
        /// Gets a user entity by email address asynchronously; returns null if not found.
        /// </summary>
        Task<User?> GetByEmailAsync(string email);

        /// <summary>
        /// 根据用户 UUID 异步获取用户实体，如果不存在则返回 null。
        /// Gets a user entity by UUID asynchronously; returns null if not found.
        /// </summary>
        Task<User?> GetByIdAsync(System.Guid id);

        /// <summary>
        /// 更新指定用户的密码哈希。
        /// Update the password hash for the specified user.
        /// </summary>
        Task UpdatePasswordAsync(System.Guid userId, string newPasswordHash);

        /// <summary>
        /// 添加新用户到存储并保存更改。
        /// Adds a new user to the store and saves changes.
        /// </summary>
        Task AddAsync(User user);
    }
}
