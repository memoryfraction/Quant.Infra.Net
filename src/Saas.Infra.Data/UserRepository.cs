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
            // Ensure Mapster mappings are registered when repository is first used
            MapsterSetup.RegisterMappings();
        }

        public UserRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(Saas.Infra.Core.User user)
        {
            // map Core.User to Data.User
            var entity = new UserEntity
            {
                UserId = user.UserId == Guid.Empty ? Guid.NewGuid() : user.UserId,
                UserName = user.Username,
                PasswordHash = user.PasswordHash,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Status = user.Status,
                CreatedTime = user.CreatedAt == default ? DateTime.UtcNow : user.CreatedAt,
                UpdatedTime = user.UpdatedTime,
                CreatedBy = user.CreatedBy,
                UpdatedBy = user.UpdatedBy,
                IsDeleted = user.IsDeleted
            };
            _db.Users.Add(entity);
            await _db.SaveChangesAsync();
            // reflect generated id back to DTO
            user.Id = entity.Id;
            user.UserId = entity.UserId;
        }


        public async Task<Saas.Infra.Core.User?> GetByUsernameAsync(string username)
        {
            // Map Data.User to Core.User DTO/contract
            var u = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == username);
            if (u == null) return null;
            return u.Adapt<Saas.Infra.Core.User>();
        }

        public async Task<Saas.Infra.Core.User?> GetByIdAsync(long id)
        {
            var u = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (u == null) return null;
            return new Saas.Infra.Core.User
            {
                Id = u.Id,
                UserId = u.UserId,
                PasswordHash = u.PasswordHash,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Status = u.Status,
                CreatedAt = u.CreatedTime,
                UpdatedTime = u.UpdatedTime,
                CreatedBy = u.CreatedBy,
                UpdatedBy = u.UpdatedBy,
                IsDeleted = u.IsDeleted
            };
        }

        public async Task UpdatePasswordAsync(long userId, string newPasswordHash)
        {
            var u = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
            if (u == null) throw new InvalidOperationException("User not found.");
            u.PasswordHash = newPasswordHash;
            await _db.SaveChangesAsync();
        }
    }
}
