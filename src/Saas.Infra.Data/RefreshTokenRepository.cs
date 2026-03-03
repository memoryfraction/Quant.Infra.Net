using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 基于 EF Core 的刷新令牌仓储实现。
    /// EF Core based refresh token repository implementation.
    /// </summary>
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly ApplicationDbContext _db;

        public RefreshTokenRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(RefreshTokenRecord record)
        {
            var entity = new RefreshToken
            {
                UserId = record.UserId,
                TokenHash = record.TokenHash,
                ExpiresAt = record.ExpiresAt,
                Revoked = record.Revoked,
                CreatedAt = record.CreatedAt,
                ReplacedByHash = record.ReplacedByHash
            };
            _db.RefreshTokens.Add(entity);
            await _db.SaveChangesAsync();
            record.Id = entity.Id;
        }

        public async Task<RefreshTokenRecord?> GetByHashAsync(string tokenHash)
        {
            var e = await _db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == tokenHash);
            if (e == null) return null;
            return new RefreshTokenRecord
            {
                Id = e.Id,
                UserId = e.UserId,
                TokenHash = e.TokenHash,
                ExpiresAt = e.ExpiresAt,
                Revoked = e.Revoked,
                CreatedAt = e.CreatedAt,
                ReplacedByHash = e.ReplacedByHash
            };
        }

        public async Task RevokeAsync(string tokenHash)
        {
            var e = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash);
            if (e == null) return;
            e.Revoked = true;
            await _db.SaveChangesAsync();
        }

        public async Task ReplaceAsync(string oldHash, RefreshTokenRecord newRecord)
        {
            // mark old revoked and insert new
            var old = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == oldHash);
            if (old != null)
            {
                old.Revoked = true;
                old.ReplacedByHash = newRecord.TokenHash;
            }

            var entity = new RefreshToken
            {
                UserId = newRecord.UserId,
                TokenHash = newRecord.TokenHash,
                ExpiresAt = newRecord.ExpiresAt,
                Revoked = newRecord.Revoked,
                CreatedAt = newRecord.CreatedAt,
                ReplacedByHash = newRecord.ReplacedByHash
            };
            _db.RefreshTokens.Add(entity);
            await _db.SaveChangesAsync();
            newRecord.Id = entity.Id;
        }
    }
}
