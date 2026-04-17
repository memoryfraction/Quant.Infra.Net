using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core.Schwab;
using Serilog;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 嘉信理财账户仓储实现。
    /// Charles Schwab account repository implementation.
    /// </summary>
    public class SchwabAccountRepository : ISchwabAccountRepository
    {
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// 初始化 <see cref="SchwabAccountRepository"/> 的新实例。
        /// Initializes a new instance of the <see cref="SchwabAccountRepository"/> class.
        /// </summary>
        /// <param name="context">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当 context 为 null 时抛出。 / Thrown when context is null.</exception>
        public SchwabAccountRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 根据用户 ID 获取所有账户。
        /// Gets all accounts by user ID.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>账户列表。 / List of accounts.</returns>
        /// <exception cref="ArgumentException">当 userId 为空时抛出。 / Thrown when userId is empty.</exception>
        public async Task<IReadOnlyList<SchwabAccountNumber>> GetByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var entities = await _context.SchwabAccounts
                    .AsNoTracking()
                    .Where(a => a.UserId == userId && !a.IsDeleted)
                    .ToListAsync();

                return entities.Select(e => new SchwabAccountNumber
                {
                    AccountNumber = e.AccountNumber,
                    HashValue = e.HashValue
                }).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get Schwab accounts for user {UserId}", userId);
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
                var entity = await _context.SchwabAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.UserId == userId && a.HashValue == hashValue && !a.IsDeleted);

                if (entity == null)
                {
                    return null;
                }

                return new SchwabAccountNumber
                {
                    AccountNumber = entity.AccountNumber,
                    HashValue = entity.HashValue
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get Schwab account by hash value for user {UserId}", userId);
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
        public async Task SaveOrUpdateAsync(Guid userId, IEnumerable<SchwabAccountNumber> accounts)
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
                var accountList = accounts.ToList();
                var existingAccounts = await _context.SchwabAccounts
                    .Where(a => a.UserId == userId)
                    .ToListAsync();

                foreach (var account in accountList)
                {
                    var existing = existingAccounts.FirstOrDefault(e => e.AccountNumber == account.AccountNumber);

                    if (existing == null)
                    {
                        // Create new
                        var entity = new SchwabAccountEntity
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            AccountNumber = account.AccountNumber,
                            HashValue = account.HashValue,
                            IsPrimary = !existingAccounts.Any(), // First account is primary
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow,
                            IsDeleted = false
                        };
                        _context.SchwabAccounts.Add(entity);
                        Log.Information("Created new Schwab account {AccountNumber} for user {UserId}", 
                            account.AccountNumber, userId);
                    }
                    else
                    {
                        // Update existing
                        existing.HashValue = account.HashValue;
                        existing.UpdatedAt = DateTimeOffset.UtcNow;
                        existing.IsDeleted = false;
                        Log.Information("Updated Schwab account {AccountNumber} for user {UserId}", 
                            account.AccountNumber, userId);
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save Schwab accounts for user {UserId}", userId);
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
        public async Task DeleteByUserIdAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User ID cannot be empty.", nameof(userId));
            }

            try
            {
                var accounts = await _context.SchwabAccounts
                    .Where(a => a.UserId == userId)
                    .ToListAsync();

                foreach (var account in accounts)
                {
                    account.IsDeleted = true;
                    account.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await _context.SaveChangesAsync();
                Log.Information("Deleted all Schwab accounts for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete Schwab accounts for user {UserId}", userId);
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
        public async Task SetPrimaryAccountAsync(Guid userId, string hashValue)
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
                var accounts = await _context.SchwabAccounts
                    .Where(a => a.UserId == userId && !a.IsDeleted)
                    .ToListAsync();

                foreach (var account in accounts)
                {
                    account.IsPrimary = account.HashValue == hashValue;
                    account.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await _context.SaveChangesAsync();
                Log.Information("Set primary Schwab account to {HashValue} for user {UserId}", hashValue, userId);
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
                var entity = await _context.SchwabAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.UserId == userId && a.IsPrimary && !a.IsDeleted);

                if (entity == null)
                {
                    return null;
                }

                return new SchwabAccountNumber
                {
                    AccountNumber = entity.AccountNumber,
                    HashValue = entity.HashValue
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get primary Schwab account for user {UserId}", userId);
                throw;
            }
        }
    }
}
