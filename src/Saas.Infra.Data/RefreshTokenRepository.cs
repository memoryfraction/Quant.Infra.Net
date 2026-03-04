using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using System;
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
            if (record == null) throw new ArgumentNullException(nameof(record));
            var entity = new RefreshToken
            {
                Id = record.Id == Guid.Empty ? Guid.NewGuid() : record.Id,
                UserId = record.UserId,
                TokenHash = record.TokenHash,
                ExpiresAt = record.ExpiresAt,
                Revoked = record.Revoked,
                CreatedTime = record.CreatedTime,
                CreatedBy = record.CreatedBy
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
                CreatedTime = e.CreatedTime,
                CreatedBy = e.CreatedBy
            };
        }

        public async Task RevokeAsync(string tokenHash)
        {
            var e = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash);
            if (e == null) return;
            e.Revoked = true;
            await _db.SaveChangesAsync();
        }
    }
}
