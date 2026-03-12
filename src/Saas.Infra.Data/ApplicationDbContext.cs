using Microsoft.EntityFrameworkCore;

namespace Saas.Infra.Data
{
    /// <summary>
    /// EF Core数据库上下文，包含所有实体的DbSet。
    /// EF Core database context containing all entity DbSets.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// 初始化<see cref="ApplicationDbContext"/>的新实例。
        /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
        /// </summary>
        /// <param name="options">数据库上下文选项。 / Database context options.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// 用户实体集合。
        /// Users entity set.
        /// </summary>
        public DbSet<UserEntity> Users { get; set; } = null!;

        /// <summary>
        /// 刷新令牌实体集合。
        /// Refresh tokens entity set.
        /// </summary>
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

        /// <summary>
        /// 产品实体集合。
        /// Products entity set.
        /// </summary>
        public DbSet<ProductEntity> Products { get; set; } = null!;

        /// <summary>
        /// 价格实体集合。
        /// Prices entity set.
        /// </summary>
        public DbSet<PriceEntity> Prices { get; set; } = null!;

        /// <summary>
        /// 订单实体集合。
        /// Orders entity set.
        /// </summary>
        public DbSet<OrderEntity> Orders { get; set; } = null!;

        /// <summary>
        /// 订阅实体集合。
        /// Subscriptions entity set.
        /// </summary>
        public DbSet<SubscriptionEntity> Subscriptions { get; set; } = null!;

        /// <summary>
        /// 角色实体集合。
        /// Roles entity set.
        /// </summary>
        public DbSet<RoleEntity> Roles { get; set; } = null!;

        /// <summary>
        /// 用户角色关联实体集合。
        /// User-role associations entity set.
        /// </summary>
        public DbSet<UserRoleEntity> UserRoles { get; set; } = null!;

        /// <summary>
        /// 支付方式实体集合。
        /// Payment methods entity set.
        /// </summary>
        public DbSet<PaymentMethodEntity> PaymentMethods { get; set; } = null!;

        /// <summary>
        /// 交易实体集合。
        /// Transactions entity set.
        /// </summary>
        public DbSet<TransactionEntity> Transactions { get; set; } = null!;

        /// <summary>
        /// 配置实体模型映射关系。
        /// Configures entity model mappings.
        /// </summary>
        /// <param name="modelBuilder">模型构建器。 / Model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // UserEntity mapping
            modelBuilder.Entity<UserEntity>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserName).HasColumnName("UserName").HasMaxLength(50).IsRequired();
                entity.HasIndex(e => e.UserName).IsUnique();
                entity.Property(e => e.PasswordHash).HasColumnName("PasswordHash").HasMaxLength(256).IsRequired();
                entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(100).IsRequired();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.PhoneNumber).HasColumnName("PhoneNumber").HasMaxLength(20);
                entity.Property(e => e.Status).HasColumnName("Status");
                entity.Property(e => e.LastLoginTime).HasColumnName("LastLoginTime");
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
                entity.Property(e => e.UpdatedTime).HasColumnName("UpdatedTime");
                entity.Property(e => e.IsDeleted).HasColumnName("IsDeleted");
            });

            // ProductEntity mapping
            modelBuilder.Entity<ProductEntity>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasColumnName("Description").HasColumnType("text");
                entity.Property(e => e.IsActive).HasColumnName("IsActive");
                entity.Property(e => e.Metadata).HasColumnName("Metadata").HasColumnType("jsonb");
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
            });

            // PriceEntity mapping
            modelBuilder.Entity<PriceEntity>(entity =>
            {
                entity.ToTable("Prices");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.ProductId).HasColumnName("ProductId").IsRequired();
                entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(100).IsRequired();
                entity.Property(e => e.BillingPeriod).HasColumnName("BillingPeriod").HasMaxLength(20).IsRequired();
                entity.Property(e => e.Amount).HasColumnName("Amount").IsRequired();
                entity.Property(e => e.Currency).HasColumnName("Currency").HasMaxLength(10);
                entity.Property(e => e.IsActive).HasColumnName("IsActive");
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
                entity.HasOne(e => e.Product)
                    .WithMany(p => p.Prices)
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // OrderEntity mapping
            modelBuilder.Entity<OrderEntity>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
                entity.Property(e => e.ProductId).HasColumnName("ProductId").IsRequired();
                entity.Property(e => e.PriceId).HasColumnName("PriceId").IsRequired();
                entity.Property(e => e.SubscriptionId).HasColumnName("SubscriptionId");
                entity.Property(e => e.OriginalAmount).HasColumnName("OriginalAmount").IsRequired();
                entity.Property(e => e.ActualAmount).HasColumnName("ActualAmount").IsRequired();
                entity.Property(e => e.DiscountAmount).HasColumnName("DiscountAmount").IsRequired();
                entity.Property(e => e.Status).HasColumnName("Status").IsRequired();
                entity.Property(e => e.ExpiredTime).HasColumnName("ExpiredTime");
                entity.Property(e => e.PaidTime).HasColumnName("PaidTime");
                entity.Property(e => e.Metadata).HasColumnName("Metadata").HasColumnType("jsonb");
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime").IsRequired();
                entity.Property(e => e.IsDeleted).HasColumnName("IsDeleted").IsRequired();
                entity.HasIndex(e => new { e.UserId, e.Status }).HasDatabaseName("IX_Orders_UserId_Status");
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Price)
                    .WithMany()
                    .HasForeignKey(e => e.PriceId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Subscription)
                    .WithMany()
                    .HasForeignKey(e => e.SubscriptionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // SubscriptionEntity mapping
            modelBuilder.Entity<SubscriptionEntity>(entity =>
            {
                entity.ToTable("Subscriptions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
                entity.Property(e => e.ProductId).HasColumnName("ProductId").IsRequired();
                entity.Property(e => e.PriceId).HasColumnName("PriceId").IsRequired();
                entity.Property(e => e.OrderId).HasColumnName("OrderId");
                entity.Property(e => e.Status).HasColumnName("Status").IsRequired();
                entity.Property(e => e.StartDate).HasColumnName("StartDate");
                entity.Property(e => e.EndDate).HasColumnName("EndDate");
                entity.Property(e => e.AutoRenew).HasColumnName("AutoRenew");
                entity.Property(e => e.Metadata).HasColumnName("Metadata").HasColumnType("jsonb");
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
                entity.Property(e => e.IsDeleted).HasColumnName("IsDeleted");
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Price)
                    .WithMany()
                    .HasForeignKey(e => e.PriceId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Order)
                    .WithMany()
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // RoleEntity mapping
            modelBuilder.Entity<RoleEntity>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(50).IsRequired();
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(255);
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
            });

            // UserRoleEntity mapping
            modelBuilder.Entity<UserRoleEntity>(entity =>
            {
                entity.ToTable("UserRoles");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
                entity.Property(e => e.RoleId).HasColumnName("RoleId").IsRequired();
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
                entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique().HasDatabaseName("UQ_UserRoles_UserId_RoleId");
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Role)
                    .WithMany()
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // RefreshToken mapping
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
                entity.Property(e => e.TokenHash).HasColumnName("TokenHash").HasMaxLength(256).IsRequired();
                entity.Property(e => e.ExpiresAt).HasColumnName("ExpiresAt").IsRequired();
                entity.Property(e => e.Revoked).HasColumnName("Revoked");
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
                entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_RefreshTokens_UserId");
                entity.HasIndex(e => e.TokenHash).HasDatabaseName("IX_RefreshTokens_TokenHash");
            });

            // PaymentMethodEntity mapping
            modelBuilder.Entity<PaymentMethodEntity>(entity =>
            {
                entity.ToTable("PaymentMethods");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
                entity.Property(e => e.Type).HasColumnName("Type").HasMaxLength(30).IsRequired();
                entity.Property(e => e.Gateway).HasColumnName("Gateway").HasMaxLength(50).IsRequired();
                entity.Property(e => e.ExternalId).HasColumnName("ExternalId").HasMaxLength(255);
                entity.Property(e => e.IsDefault).HasColumnName("IsDefault");
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_PaymentMethods_UserId");
                entity.HasIndex(e => e.Gateway).HasDatabaseName("IX_PaymentMethods_Gateway");
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TransactionEntity mapping
            modelBuilder.Entity<TransactionEntity>(entity =>
            {
                entity.ToTable("Transactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("Id");
                entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
                entity.Property(e => e.OrderId).HasColumnName("OrderId");
                entity.Property(e => e.SubscriptionId).HasColumnName("SubscriptionId");
                entity.Property(e => e.Amount).HasColumnName("Amount").IsRequired();
                entity.Property(e => e.Currency).HasColumnName("Currency").HasMaxLength(10).IsRequired();
                entity.Property(e => e.Gateway).HasColumnName("Gateway").HasMaxLength(50).IsRequired();
                entity.Property(e => e.ExternalTransactionId).HasColumnName("ExternalTransactionId").HasMaxLength(255);
                entity.Property(e => e.Status).HasColumnName("Status").IsRequired();
                entity.Property(e => e.CreatedTime).HasColumnName("CreatedTime");
                entity.Property(e => e.Remarks).HasColumnName("Remarks").HasColumnType("text");
                entity.HasIndex(e => new { e.OrderId, e.Status }).HasDatabaseName("IX_Transactions_OrderId_Status");
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Order)
                    .WithMany()
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Subscription)
                    .WithMany()
                    .HasForeignKey(e => e.SubscriptionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}

