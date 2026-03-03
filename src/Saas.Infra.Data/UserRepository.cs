using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using System.Threading.Tasks;

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

        public async Task AddAsync(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        public Task AddAsync(Core.User user)
        {
            throw new NotImplementedException();
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
    }
}
