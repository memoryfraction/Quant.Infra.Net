using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;

namespace Saas.Infra.MVC.Services.Product;

/// <summary>
/// 产品配置服务，从数据库读取可用产品信息。
/// Product configuration service that reads available products from the database.
/// </summary>
public class ProductConfigService : IProductConfigService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// 初始化<see cref="ProductConfigService"/>的新实例。
    /// Initializes a new instance of the <see cref="ProductConfigService"/> class.
    /// </summary>
    /// <param name="db">应用程序数据库上下文。 / Application database context.</param>
    /// <exception cref="ArgumentNullException">当db为null时抛出。 / Thrown when db is null.</exception>
    public ProductConfigService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 获取用户所有可用的产品列表。
    /// Gets all available products for the user.
    /// </summary>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <returns>产品信息列表的任务。 / Task containing list of product information.</returns>
    /// <exception cref="ArgumentNullException">当userId为null或空白时抛出。 / Thrown when userId is null or whitespace.</exception>
    public async Task<List<ProductInfo>> GetAvailableProductsAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentNullException(nameof(userId));

        var result = new List<ProductInfo>();

        try
        {
            var products = await _db.Products
                .Where(p => p.IsActive)
                .Select(p => new ProductInfo
                {
                    Id = p.Code,
                    Name = p.Name,
                    Description = p.Description,
                    Metadata = p.Metadata
                })
                .ToListAsync();

            return products;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to load available products for user {UserId}", userId);
            return new List<ProductInfo>();
        }
    }

    /// <summary>
    /// 根据ID获取特定产品信息（不区分大小写）。
    /// Gets a specific product by ID (case-insensitive).
    /// </summary>
    /// <param name="productId">产品标识。 / Product identifier.</param>
    /// <returns>产品信息的任务（如果未找到则返回null）。 / Task containing product information (null if not found).</returns>
    /// <exception cref="ArgumentNullException">当productId为null或空白时抛出。 / Thrown when productId is null or whitespace.</exception>
    public async Task<ProductInfo?> GetProductAsync(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentNullException(nameof(productId));

        try
        {
            var product = await _db.Products
                .Where(p => p.Code.ToLower() == productId.ToLower())
                .Select(p => new ProductInfo
                {
                    Id = p.Code,
                    Name = p.Name,
                    Description = p.Description,
                    Metadata = p.Metadata
                })
                .FirstOrDefaultAsync();

            return product;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to load product {ProductId}", productId);
            return null;
        }
    }
}
