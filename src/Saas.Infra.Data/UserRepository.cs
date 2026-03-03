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

        public UserRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(Saas.Infra.Core.User user)
        {
            // map Core.User to Data.User
            var entity = new User
            {
                Username = user.Username,
                PasswordHash = user.PasswordHash,
                DisplayName = user.DisplayName,
                CreatedAt = user.CreatedAt
            };
            _db.Users.Add(entity);
            await _db.SaveChangesAsync();
            // reflect generated id back to DTO
            user.Id = entity.Id;
        }


        public async Task<Saas.Infra.Core.User?> GetByUsernameAsync(string username)
        {
            // Map Data.User to Core.User DTO/contract
            var u = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Username == username);
            if (u == null) return null;
            return new Saas.Infra.Core.User
            {
                Id = u.Id,
                Username = u.Username,
                PasswordHash = u.PasswordHash,
                DisplayName = u.DisplayName,
                CreatedAt = u.CreatedAt
            };
        }

        public async Task<Saas.Infra.Core.User?> GetByIdAsync(int id)
        {
            var u = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (u == null) return null;
            return new Saas.Infra.Core.User
            {
                Id = u.Id,
                Username = u.Username,
                PasswordHash = u.PasswordHash,
                DisplayName = u.DisplayName,
                CreatedAt = u.CreatedAt
            };
        }

        public async Task UpdatePasswordAsync(int userId, string newPasswordHash)
        {
            var u = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId);
            if (u == null) throw new InvalidOperationException("User not found.");
            u.PasswordHash = newPasswordHash;
            await _db.SaveChangesAsync();
        }
    }
}
