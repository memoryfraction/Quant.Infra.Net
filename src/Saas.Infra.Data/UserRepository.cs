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

        public UserRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(Saas.Infra.Core.User user)
        {
            var entity = new UserEntity
            {
                Id = user.Id == Guid.Empty ? Guid.NewGuid() : user.Id,
                UserName = user.Username,
                PasswordHash = user.PasswordHash,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Status = user.Status,
                LastLoginTime = user.LastLoginTime,
                CreatedTime = user.CreatedTime == default ? DateTime.UtcNow : user.CreatedTime,
                UpdatedTime = user.UpdatedTime,
                CreatedBy = user.CreatedBy,
                UpdatedBy = user.UpdatedBy,
                IsDeleted = user.IsDeleted
            };
            _db.Users.Add(entity);
            await _db.SaveChangesAsync();
            user.Id = entity.Id;
        }

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
                Status = u.Status,
                LastLoginTime = u.LastLoginTime,
                CreatedTime = u.CreatedTime,
                UpdatedTime = u.UpdatedTime,
                CreatedBy = u.CreatedBy,
                UpdatedBy = u.UpdatedBy,
                IsDeleted = u.IsDeleted
            };
        }

        /// <summary>
        /// 根据电子邮件地址获取用户。
        /// Get user by email address.
        /// </summary>
        /// <param name="email">电子邮件地址 / Email address</param>
        /// <returns>用户对象或null / User object or null</returns>
        /// <exception cref="ArgumentException">当email为null或空白时抛出 / Thrown when email is null or whitespace</exception>
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
                Status = u.Status,
                LastLoginTime = u.LastLoginTime,
                CreatedTime = u.CreatedTime,
                UpdatedTime = u.UpdatedTime,
                CreatedBy = u.CreatedBy,
                UpdatedBy = u.UpdatedBy,
                IsDeleted = u.IsDeleted
            };
        }

        public async Task<Saas.Infra.Core.User?> GetByIdAsync(Guid id)
        {
            var u = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (u == null) return null;
            
            return new Saas.Infra.Core.User
            {
                Id = u.Id,
                Username = u.UserName,
                PasswordHash = u.PasswordHash,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = u.Status,
                LastLoginTime = u.LastLoginTime,
                CreatedTime = u.CreatedTime,
                UpdatedTime = u.UpdatedTime,
                CreatedBy = u.CreatedBy,
                UpdatedBy = u.UpdatedBy,
                IsDeleted = u.IsDeleted
            };
        }

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
    }
}
