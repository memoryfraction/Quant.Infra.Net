using Microsoft.EntityFrameworkCore;

namespace Saas.Infra.Data
{
    /// <summary>
    /// EF Core DbContext，包含 Users 表的 DbSet。
    /// EF Core DbContext containing the Users DbSet.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    }
}
