using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Saas.Infra.Core.Schwab;
using Serilog;

namespace Saas.Infra.Services.Schwab
{
    /// <summary>
    /// 嘉信理财账户内存缓存仓储实现。
    /// Charles Schwab account memory cache repository implementation.
    /// </summary>
    public class SchwabAccountMemoryCacheRepository : ISchwabAccountRepository
    {
        private readonly IMemoryCache _cache;
        private const string CacheKeyPrefix = "schwab_accounts_";
        private const string PrimaryCacheKeyPrefix = "schwab_primary_account_";

        /// <summary>
        /// 初始化 <see cref="SchwabAccountMemoryCacheRepository"/> 的新实例。
        /// Initializes a new instance of the <see cref="SchwabAccountMemoryCacheRepository"/> class.
        /// </summary>
        /// <param name="cache">内存缓存。 / Memory cache.</param>
        /// <exception cref="ArgumentNullException">当 cache 为 null 时抛出。 / Thrown when cache is null.</exception>
        public SchwabAccountMemoryCacheRepository(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// 根据用户 ID 获取所有账户。
        /// Gets all accounts by user ID.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>账户列表。 / List of accounts.</returns>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public Task<IReadOnlyList<SchwabAccountNumber>> GetByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var cacheKey = GetAccountsCacheKey(userId);
                var accounts = _cache.Get<List<SchwabAccountNumber>>(cacheKey) ?? new List<SchwabAccountNumber>();
                
                Log.Debug("Retrieved {Count} Schwab accounts from cache for user {UserId}", accounts.Count, userId);
                
                return Task.FromResult<IReadOnlyList<SchwabAccountNumber>>(accounts);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get Schwab accounts from cache for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 根据账户哈希值获取账户。
        /// Gets account by hash value.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="hashValue">账户哈希值。 / Account hash value.</param>
        /// <returns>账户信息，如果不存在则返回 null。 / Account info, or null if not found.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        public async Task<SchwabAccountNumber?> GetByHashValueAsync(Guid userId, string hashValue)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(hashValue))
            {
                throw new ArgumentException("Hash value cannot be empty.", nameof(hashValue));
            }

            try
            {
                var accounts = await GetByUserIdAsync(userId);
                return accounts.FirstOrDefault(a => a.HashValue == hashValue);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get Schwab account by hash value from cache for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 保存或更新账户列表。
        /// Saves or updates account list.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="accounts">账户列表。 / List of accounts.</param>
        /// <returns>异步任务。 / Async task.</returns>
        /// <exception cref="ArgumentNullException">当 accounts 为 null 时抛出。 / Thrown when accounts is null.</exception>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public Task SaveOrUpdateAsync(Guid userId, IEnumerable<SchwabAccountNumber> accounts)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            if (accounts == null)
            {
                throw new ArgumentNullException(nameof(accounts));
            }

            try
            {
                var cacheKey = GetAccountsCacheKey(userId);
                var accountList = accounts.ToList();
                
                // 缓存1天（账户信息相对稳定）
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, accountList, cacheOptions);
                Log.Information("Saved {Count} Schwab accounts to cache for user {UserId}", accountList.Count, userId);
                
                // 如果没有主账户，设置第一个为主账户
                var primaryAccount = _cache.Get<string>(GetPrimaryCacheKey(userId));
                if (string.IsNullOrEmpty(primaryAccount) && accountList.Any())
                {
                    _cache.Set(GetPrimaryCacheKey(userId), accountList.First().HashValue, cacheOptions);
                }
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save Schwab accounts to cache for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 删除用户的所有账户。
        /// Deletes all accounts for user.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>异步任务。 / Async task.</returns>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public Task DeleteByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                _cache.Remove(GetAccountsCacheKey(userId));
                _cache.Remove(GetPrimaryCacheKey(userId));
                Log.Information("Deleted all Schwab accounts from cache for user {UserId}", userId);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete Schwab accounts from cache for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 设置主账户。
        /// Sets primary account.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="hashValue">账户哈希值。 / Account hash value.</param>
        /// <returns>异步任务。 / Async task.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        public Task SetPrimaryAccountAsync(Guid userId, string hashValue)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(hashValue))
            {
                throw new ArgumentException("Hash value cannot be empty.", nameof(hashValue));
            }

            try
            {
                var cacheKey = GetPrimaryCacheKey(userId);
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(cacheKey, hashValue, cacheOptions);
                Log.Information("Set primary Schwab account to {HashValue} for user {UserId}", hashValue, userId);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set primary Schwab account for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 获取主账户。
        /// Gets primary account.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>主账户信息，如果不存在则返回 null。 / Primary account info, or null if not found.</returns>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public async Task<SchwabAccountNumber?> GetPrimaryAccountAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var primaryHashValue = _cache.Get<string>(GetPrimaryCacheKey(userId));
                
                if (string.IsNullOrEmpty(primaryHashValue))
                {
                    // 如果没有设置主账户，返回第一个账户
                    var accounts = await GetByUserIdAsync(userId);
                    return accounts.FirstOrDefault();
                }

                return await GetByHashValueAsync(userId, primaryHashValue);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get primary Schwab account from cache for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// 获取账户列表缓存键。
        /// Gets accounts cache key.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>缓存键。 / Cache key.</returns>
        private static string GetAccountsCacheKey(Guid userId)
        {
            return $"{CacheKeyPrefix}{userId}";
        }

        /// <summary>
        /// 获取主账户缓存键。
        /// Gets primary account cache key.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>缓存键。 / Cache key.</returns>
        private static string GetPrimaryCacheKey(Guid userId)
        {
            return $"{PrimaryCacheKeyPrefix}{userId}";
        }
    }
}
