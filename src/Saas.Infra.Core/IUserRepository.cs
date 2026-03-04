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
        /// <param name="username">用户名，不能为空或空白。 / The username; cannot be null or whitespace.</param>
        /// <summary>
        /// Get a user by username.
        /// </summary>
        Task<User?> GetByUsernameAsync(string username);

        /// <summary>
        /// 根据用户 Id 异步获取用户实体，如果不存在则返回 null。
        /// Gets a user entity by id asynchronously; returns null if not found.
        /// </summary>
        /// <summary>
        /// Get a user by numeric id.
        /// </summary>
        Task<User?> GetByIdAsync(long id);

        /// <summary>
        /// 更新指定用户的密码哈希。
        /// Update the password hash for the specified user.
        /// </summary>
        /// <param name="userId">用户 Id。</param>
        /// <param name="newPasswordHash">新的密码哈希。</param>
        Task UpdatePasswordAsync(long userId, string newPasswordHash);

        /// <summary>
        /// 添加新用户到存储并保存更改。
        /// Adds a new user to the store and saves changes.
        /// </summary>
        /// <param name="user">要添加的用户实体。 / The user entity to add.</param>
        /// <summary>
        /// Add a new user to the store and persist changes.
        /// </summary>
        Task AddAsync(User user);
    }
}
