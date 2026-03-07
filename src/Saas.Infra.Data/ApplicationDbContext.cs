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

        public DbSet<UserEntity> Users { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<ProductEntity> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // 配置 UserEntity 映射，确保不查询 UserId 列
            modelBuilder.Entity<UserEntity>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserName).HasColumnName("UserName");
                entity.Property(e => e.PasswordHash).HasColumnName("PasswordHash");
                entity.Property(e => e.Email).HasColumnName("Email");
                entity.Property(e => e.PhoneNumber).HasColumnName("PhoneNumber");
                entity.Property(e => e.Status).HasColumnName("Status");
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
                entity.Property(e => e.UpdatedTime).HasColumnName("UpdatedTime");
                entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
                entity.Property(e => e.UpdatedBy).HasColumnName("UpdatedBy");
                entity.Property(e => e.IsDeleted).HasColumnName("IsDeleted");
            });

            // Product entity mapping
            modelBuilder.Entity<ProductEntity>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.Name).HasColumnName("Name");
                entity.Property(e => e.Url).HasColumnName("Url");
                entity.Property(e => e.IconUrl).HasColumnName("IconUrl");
                entity.Property(e => e.Description).HasColumnName("Description");
            });
        }
    }
}
