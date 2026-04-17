using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Saas.Infra.Core.Schwab;
using Serilog;

namespace Saas.Infra.Services.Schwab
{
    /// <summary>
    /// 嘉信理财令牌内存缓存仓储实现。
    /// Charles Schwab token memory cache repository implementation.
    /// </summary>
    public class SchwabTokenMemoryCacheRepository : ISchwabTokenRepository
    {
        private readonly IMemoryCache _cache;
        private const string CacheKeyPrefix = "schwab_token_";

        /// <summary>
        /// 初始化 <see cref="SchwabTokenMemoryCacheRepository"/> 的新实例。
        /// Initializes a new instance of the <see cref="SchwabTokenMemoryCacheRepository"/> class.
        /// </summary>
        /// <param name="cache">内存缓存。 / Memory cache.</param>
        /// <exception cref="ArgumentNullException">当 cache 为 null 时抛出。 / Thrown when cache is null.</exception>
        public SchwabTokenMemoryCacheRepository(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// 根据用户 ID 获取令牌。
        /// Gets token by user ID.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>令牌响应，如果不存在则返回 null。 / Token response, or null if not found.</returns>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public Task<SchwabTokenResponse?> GetByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var cacheKey = GetCacheKey(userId);
                var token = _cache.Get<SchwabTokenResponse>(cacheKey);
                
                if (token != null)
                {
                    Log.Debug("Retrieved Schwab token from cache for user {UserId}", userId);
                }
                
                return Task.FromResult(token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get Schwab token from cache for user {UserId}", userId);
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
        public Task SaveOrUpdateAsync(Guid userId, SchwabTokenResponse tokenResponse)
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
                var cacheKey = GetCacheKey(userId);
                
                // 缓存7天（Refresh Token 有效期）
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    Priority = CacheItemPriority.High
                };

                _cache.Set(cacheKey, tokenResponse, cacheOptions);
                Log.Information("Saved Schwab token to cache for user {UserId}, expires at {ExpiresAt}", 
                    userId, tokenResponse.ExpiresAt);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save Schwab token to cache for user {UserId}", userId);
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
        public Task DeleteAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var cacheKey = GetCacheKey(userId);
                _cache.Remove(cacheKey);
                Log.Information("Deleted Schwab token from cache for user {UserId}", userId);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete Schwab token from cache for user {UserId}", userId);
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
        public Task<bool> IsAuthorizedAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var cacheKey = GetCacheKey(userId);
                var exists = _cache.TryGetValue(cacheKey, out SchwabTokenResponse? _);
                return Task.FromResult(exists);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check Schwab authorization from cache for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 获取缓存键。
        /// Gets cache key.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>缓存键。 / Cache key.</returns>
        private static string GetCacheKey(Guid userId)
        {
            return $"{CacheKeyPrefix}{userId}";
        }
    }
}
