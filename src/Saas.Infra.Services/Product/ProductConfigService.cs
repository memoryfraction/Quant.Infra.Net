using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Serilog.Events;

namespace Saas.Infra.Services.Product;

public interface IProductConfigService
{
    Task<List<ProductInfo>> GetAvailableProductsAsync(string userId);
    Task<ProductInfo?> GetProductAsync(string productId);
}

public class ProductInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Metadata { get; set; }
}

public class ProductConfigService : IProductConfigService
{
    private readonly ApplicationDbContext _db;

    public ProductConfigService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<List<ProductInfo>> GetAvailableProductsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentNullException(nameof(userId));

        try
        {
            return await _db.Products
                .Where(p => p.IsActive)
                .Select(p => new ProductInfo
                {
                    Id = p.Code,
                    Name = p.Name,
                    Description = p.Description,
                    Metadata = p.Metadata
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            UtilityService.LogAndWriteLine(ex, LogEventLevel.Warning, "Failed to load available products for user {UserId}", userId);
            return new List<ProductInfo>();
        }
    }

    public async Task<ProductInfo?> GetProductAsync(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentNullException(nameof(productId));

        try
        {
            return await _db.Products
                .Where(p => p.Code.ToLower() == productId.ToLower())
                .Select(p => new ProductInfo
                {
                    Id = p.Code,
                    Name = p.Name,
                    Description = p.Description,
                    Metadata = p.Metadata
                })
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            UtilityService.LogAndWriteLine(ex, LogEventLevel.Warning, "Failed to load product {ProductId}", productId);
            return null;
        }
    }
}
