using Mapster;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 基于 EF Core 的用户仓储实现。
    /// EF Core based user repository implementation.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _db;

        static UserRepository()
        {
            MapsterSetup.RegisterMappings();
        }

        /// <summary>
        /// 初始化<see cref="UserRepository"/>的新实例。
        /// Initializes a new instance of the <see cref="UserRepository"/> class.
        /// </summary>
        /// <param name="db">应用程序数据库上下文。 / Application database context.</param>
        /// <exception cref="ArgumentNullException">当db为null时抛出。 / Thrown when db is null.</exception>
        public UserRepository(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 添加新用户到数据库。
        /// Adds a new user to the database.
        /// </summary>
        /// <param name="user">用户领域模型。 / User domain model.</param>
        /// <returns>异步任务。 / Asynchronous task.</returns>
        /// <exception cref="ArgumentNullException">当user为null时抛出。 / Thrown when user is null.</exception>
        public async Task AddAsync(Saas.Infra.Core.User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var entity = new UserEntity
            {
                Id = user.Id == Guid.Empty ? Guid.NewGuid() : user.Id,
                UserName = user.Username,
                PasswordHash = user.PasswordHash,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Status = (short)user.Status,
                LastLoginTime = user.LastLoginTime,
                CreatedTime = user.CreatedTime == default ? DateTimeOffset.UtcNow : user.CreatedTime,
                UpdatedTime = user.UpdatedTime,
                IsDeleted = user.IsDeleted
            };
            _db.Users.Add(entity);
            await _db.SaveChangesAsync();
            user.Id = entity.Id;
        }

        /// <summary>
        /// 根据用户名获取用户。
        /// Gets user by username.
        /// </summary>
        /// <param name="username">用户名。 / Username.</param>
        /// <returns>用户领域模型或null。 / User domain model or null.</returns>
        /// <exception cref="ArgumentException">当username为null或空白时抛出。 / Thrown when username is null or whitespace.</exception>
        public async Task<Saas.Infra.Core.User?> GetByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) 
                throw new ArgumentException("username must not be null or whitespace", nameof(username));

            var u = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == username);
            if (u == null) return null;

            return new Saas.Infra.Core.User
            {
                Id = u.Id,
                Username = u.UserName,
                PasswordHash = u.PasswordHash,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = (UserStatus)u.Status,
                LastLoginTime = u.LastLoginTime,
                CreatedTime = u.CreatedTime,
                UpdatedTime = u.UpdatedTime,
                IsDeleted = u.IsDeleted
            };
        }

        /// <summary>
        /// 根据电子邮件地址获取用户。
        /// Gets user by email address.
        /// </summary>
        /// <param name="email">电子邮件地址。 / Email address.</param>
        /// <returns>用户领域模型或null。 / User domain model or null.</returns>
        /// <exception cref="ArgumentException">当email为null或空白时抛出。 / Thrown when email is null or whitespace.</exception>
        public async Task<Saas.Infra.Core.User?> GetByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("email must not be null or whitespace", nameof(email));

            var u = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email);
            if (u == null) return null;

            return new Saas.Infra.Core.User
            {
                Id = u.Id,
                Username = u.UserName,
                PasswordHash = u.PasswordHash,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = (UserStatus)u.Status,
                LastLoginTime = u.LastLoginTime,
                CreatedTime = u.CreatedTime,
                UpdatedTime = u.UpdatedTime,
                IsDeleted = u.IsDeleted
            };
        }

        /// <summary>
        /// 根据用户ID获取用户。
        /// Gets user by ID.
        /// </summary>
        /// <param name="id">用户ID。 / User ID.</param>
        /// <returns>用户领域模型或null。 / User domain model or null.</returns>
        /// <exception cref="ArgumentException">当id为空GUID时抛出。 / Thrown when id is empty GUID.</exception>
        public async Task<Saas.Infra.Core.User?> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("id must be a valid UUID", nameof(id));

            var u = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (u == null) return null;

            return new Saas.Infra.Core.User
            {
                Id = u.Id,
                Username = u.UserName,
                PasswordHash = u.PasswordHash,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = (UserStatus)u.Status,
                LastLoginTime = u.LastLoginTime,
                CreatedTime = u.CreatedTime,
                UpdatedTime = u.UpdatedTime,
                IsDeleted = u.IsDeleted
            };
        }

        /// <summary>
        /// 更新用户密码。
        /// Updates user password.
        /// </summary>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="newPasswordHash">新密码哈希。 / New password hash.</param>
        /// <returns>异步任务。 / Asynchronous task.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        /// <exception cref="InvalidOperationException">当用户不存在时抛出。 / Thrown when user not found.</exception>
        public async Task UpdatePasswordAsync(Guid userId, string newPasswordHash)
        {
            if (userId == Guid.Empty) 
                throw new ArgumentException("userId must be a valid UUID", nameof(userId));
            if (string.IsNullOrWhiteSpace(newPasswordHash)) 
                throw new ArgumentException("newPasswordHash must not be null or whitespace", nameof(newPasswordHash));

            var u = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
            if (u == null) throw new InvalidOperationException("User not found.");

            u.PasswordHash = newPasswordHash;
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// 根据用户ID获取角色代码列表。
        /// Gets role code list by user ID.
        /// </summary>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <returns>角色代码列表。 / Role code list.</returns>
        /// <exception cref="ArgumentException">当 userId 为空 GUID 时抛出。 / Thrown when userId is empty GUID.</exception>
        public async Task<IReadOnlyList<string>> GetRoleCodesByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("userId must be a valid UUID", nameof(userId));

            var roleCodes = await _db.UserRoles
                .AsNoTracking()
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role != null ? ur.Role.Code : null)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .Distinct()
                .ToListAsync();

            return roleCodes;
        }
    }
}
