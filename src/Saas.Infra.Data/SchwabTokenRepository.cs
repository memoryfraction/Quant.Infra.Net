using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core.Schwab;
using Serilog;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 嘉信理财令牌仓储实现。
    /// Charles Schwab token repository implementation.
    /// </summary>
    public class SchwabTokenRepository : ISchwabTokenRepository
    {
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// 初始化 <see cref="SchwabTokenRepository"/> 的新实例。
        /// Initializes a new instance of the <see cref="SchwabTokenRepository"/> class.
        /// </summary>
        /// <param name="context">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当 context 为 null 时抛出。 / Thrown when context is null.</exception>
        public SchwabTokenRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 根据用户 ID 获取令牌。
        /// Gets token by user ID.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>令牌响应，如果不存在则返回 null。 / Token response, or null if not found.</returns>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public async Task<SchwabTokenResponse?> GetByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var entity = await _context.SchwabTokens
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.UserId == userId && !t.IsDeleted);

                if (entity == null)
                {
                    return null;
                }

                return new SchwabTokenResponse
                {
                    AccessToken = entity.AccessToken,
                    RefreshToken = entity.RefreshToken,
                    TokenType = entity.TokenType,
                    ExpiresIn = entity.ExpiresIn,
                    Scope = entity.Scope,
                    CreatedAt = entity.CreatedAt
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get Schwab token for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 保存或更新令牌。
        /// Saves or updates token.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="tokenResponse">令牌响应。 / Token response.</param>
        /// <returns>异步任务。 / Async task.</returns>
        /// <exception cref="ArgumentNullException">当 tokenResponse 为 null 时抛出。 / Thrown when tokenResponse is null.</exception>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public async Task SaveOrUpdateAsync(Guid userId, SchwabTokenResponse tokenResponse)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            if (tokenResponse == null)
            {
                throw new ArgumentNullException(nameof(tokenResponse));
            }

            try
            {
                var entity = await _context.SchwabTokens
                    .FirstOrDefaultAsync(t => t.UserId == userId);

                if (entity == null)
                {
                    // Create new
                    entity = new SchwabTokenEntity
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        AccessToken = tokenResponse.AccessToken,
                        RefreshToken = tokenResponse.RefreshToken,
                        TokenType = tokenResponse.TokenType,
                        ExpiresIn = tokenResponse.ExpiresIn,
                        Scope = tokenResponse.Scope,
                        CreatedAt = tokenResponse.CreatedAt,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        IsDeleted = false
                    };
                    _context.SchwabTokens.Add(entity);
                    Log.Information("Created new Schwab token for user {UserId}", userId);
                }
                else
                {
                    // Update existing
                    entity.AccessToken = tokenResponse.AccessToken;
                    entity.RefreshToken = tokenResponse.RefreshToken;
                    entity.TokenType = tokenResponse.TokenType;
                    entity.ExpiresIn = tokenResponse.ExpiresIn;
                    entity.Scope = tokenResponse.Scope;
                    entity.CreatedAt = tokenResponse.CreatedAt;
                    entity.UpdatedAt = DateTimeOffset.UtcNow;
                    entity.IsDeleted = false;
                    Log.Information("Updated Schwab token for user {UserId}", userId);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save Schwab token for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 删除用户的令牌。
        /// Deletes user's token.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>异步任务。 / Async task.</returns>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public async Task DeleteAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var entity = await _context.SchwabTokens
                    .FirstOrDefaultAsync(t => t.UserId == userId);

                if (entity != null)
                {
                    entity.IsDeleted = true;
                    entity.UpdatedAt = DateTimeOffset.UtcNow;
                    await _context.SaveChangesAsync();
                    Log.Information("Deleted Schwab token for user {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete Schwab token for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 检查用户是否已授权。
        /// Checks if user is authorized.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>是否已授权。 / Whether authorized.</returns>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public async Task<bool> IsAuthorizedAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                return await _context.SchwabTokens
                    .AnyAsync(t => t.UserId == userId && !t.IsDeleted);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check Schwab authorization for user {UserId}", userId);
                throw;
            }
        }
    }
}
