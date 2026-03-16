using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;

namespace Saas.Infra.Services.Product;

/// <summary>
/// 产品应用服务抽象。
/// Product application service abstraction.
/// </summary>
public interface IProductApplicationService
{
    /// <summary>
    /// 获取产品列表。
    /// Gets the product list.
    /// </summary>
    /// <param name="activeOnly">是否仅返回激活产品。 / Whether to return active products only.</param>
    /// <param name="isAdmin">当前调用方是否为管理员。 / Whether the current caller is an administrator.</param>
    /// <returns>产品列表。 / Product list.</returns>
    Task<List<ProductEntity>> GetProductsAsync(bool activeOnly, bool isAdmin);

    /// <summary>
    /// 根据标识获取产品。
    /// Gets a product by identifier.
    /// </summary>
    /// <param name="id">产品标识。 / Product identifier.</param>
    /// <returns>产品实体。 / Product entity.</returns>
    Task<ProductEntity?> GetProductByIdAsync(Guid id);

    /// <summary>
    /// 创建产品。
    /// Creates a product.
    /// </summary>
    /// <param name="code">产品编码。 / Product code.</param>
    /// <param name="name">产品名称。 / Product name.</param>
    /// <param name="description">产品描述。 / Product description.</param>
    /// <param name="isActive">是否激活。 / Whether the product is active.</param>
    /// <param name="metadata">元数据。 / Metadata.</param>
    /// <returns>产品实体。 / Product entity.</returns>
    Task<ProductEntity> CreateProductAsync(string code, string name, string? description, bool isActive, string? metadata);

    /// <summary>
    /// 更新产品。
    /// Updates a product.
    /// </summary>
    /// <param name="id">产品标识。 / Product identifier.</param>
    /// <param name="name">产品名称。 / Product name.</param>
    /// <param name="description">产品描述。 / Product description.</param>
    /// <param name="isActive">是否激活。 / Whether the product is active.</param>
    /// <param name="metadata">元数据。 / Metadata.</param>
    /// <returns>产品实体。 / Product entity.</returns>
    Task<ProductEntity?> UpdateProductAsync(Guid id, string? name, string? description, bool? isActive, string? metadata);

    /// <summary>
    /// 软删除产品。
    /// Soft-deletes a product.
    /// </summary>
    /// <param name="id">产品标识。 / Product identifier.</param>
    /// <returns>产品实体。 / Product entity.</returns>
    Task<ProductEntity?> SoftDeleteProductAsync(Guid id);

    /// <summary>
    /// 获取产品下的价格列表。
    /// Gets the prices for a product.
    /// </summary>
    /// <param name="productId">产品标识。 / Product identifier.</param>
    /// <returns>价格列表。 / Price list.</returns>
    Task<List<PriceEntity>> GetPricesByProductAsync(Guid productId);

    /// <summary>
    /// 根据标识获取价格。
    /// Gets a price by identifier.
    /// </summary>
    /// <param name="id">价格标识。 / Price identifier.</param>
    /// <returns>价格实体。 / Price entity.</returns>
    Task<PriceEntity?> GetPriceByIdAsync(Guid id);

    /// <summary>
    /// 创建价格。
    /// Creates a price.
    /// </summary>
    /// <param name="productId">产品标识。 / Product identifier.</param>
    /// <param name="name">价格名称。 / Price name.</param>
    /// <param name="billingPeriod">计费周期。 / Billing period.</param>
    /// <param name="amount">金额。 / Amount.</param>
    /// <param name="currency">货币代码。 / Currency code.</param>
    /// <param name="isActive">是否激活。 / Whether the price is active.</param>
    /// <returns>价格实体。 / Price entity.</returns>
    Task<PriceEntity> CreatePriceAsync(Guid productId, string name, string billingPeriod, long amount, string currency, bool isActive);

    /// <summary>
    /// 更新价格。
    /// Updates a price.
    /// </summary>
    /// <param name="id">价格标识。 / Price identifier.</param>
    /// <param name="name">价格名称。 / Price name.</param>
    /// <param name="amount">金额。 / Amount.</param>
    /// <param name="isActive">是否激活。 / Whether the price is active.</param>
    /// <returns>价格实体。 / Price entity.</returns>
    Task<PriceEntity?> UpdatePriceAsync(Guid id, string? name, long? amount, bool? isActive);

    /// <summary>
    /// 软删除价格。
    /// Soft-deletes a price.
    /// </summary>
    /// <param name="id">价格标识。 / Price identifier.</param>
    /// <returns>价格实体。 / Price entity.</returns>
    Task<PriceEntity?> SoftDeletePriceAsync(Guid id);

    /// <summary>
    /// 获取激活产品列表。
    /// Gets active products.
    /// </summary>
    /// <returns>产品列表。 / Product list.</returns>
    Task<List<ProductEntity>> GetActiveProductsAsync();

    /// <summary>
    /// 获取激活产品详情。
    /// Gets active product details.
    /// </summary>
    /// <param name="id">产品标识。 / Product identifier.</param>
    /// <returns>产品实体。 / Product entity.</returns>
    Task<ProductEntity?> GetActiveProductDetailsAsync(Guid id);

    /// <summary>
    /// 获取激活价格及产品。
    /// Gets an active price with product information.
    /// </summary>
    /// <param name="priceId">价格标识。 / Price identifier.</param>
    /// <returns>价格实体。 / Price entity.</returns>
    Task<PriceEntity?> GetActivePriceWithProductAsync(Guid priceId);
}

/// <summary>
/// 产品应用服务。
/// Product application service。
/// </summary>
public class ProductApplicationService : IProductApplicationService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// 初始化产品应用服务。
    /// Initializes the product application service.
    /// </summary>
    /// <param name="db">数据库上下文。 / Database context.</param>
    public ProductApplicationService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <inheritdoc />
    public Task<List<ProductEntity>> GetProductsAsync(bool activeOnly, bool isAdmin)
    {
        var query = _db.Products.AsNoTracking();
        if (activeOnly || !isAdmin)
        {
            query = query.Where(p => p.IsActive);
        }

        return query
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();
    }

    /// <inheritdoc />
    public Task<ProductEntity?> GetProductByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(id));
        }

        return _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <inheritdoc />
    public async Task<ProductEntity> CreateProductAsync(string code, string name, string? description, bool isActive, string? metadata)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Product code cannot be null or whitespace.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name cannot be null or whitespace.", nameof(name));
        }

        var normalizedCode = code.Trim();
        var normalizedName = name.Trim();
        var exists = await _db.Products.AnyAsync(p => p.Code == normalizedCode);
        if (exists)
        {
            throw new InvalidOperationException("Product code already exists.");
        }

        var product = new ProductEntity
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = isActive,
            Metadata = string.IsNullOrWhiteSpace(metadata) ? null : metadata.Trim(),
            CreatedTime = DateTimeOffset.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    /// <inheritdoc />
    public async Task<ProductEntity?> UpdateProductAsync(Guid id, string? name, string? description, bool? isActive, string? metadata)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(id));
        }

        var product = await _db.Products.FindAsync(id);
        if (product == null)
        {
            return null;
        }

        if (name != null)
            product.Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Product name cannot be empty when provided.", nameof(name)) : name.Trim();
        if (description != null)
            product.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (isActive.HasValue)
            product.IsActive = isActive.Value;
        if (metadata != null)
            product.Metadata = string.IsNullOrWhiteSpace(metadata) ? null : metadata.Trim();

        await _db.SaveChangesAsync();
        return product;
    }

    /// <inheritdoc />
    public async Task<ProductEntity?> SoftDeleteProductAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(id));
        }

        var product = await _db.Products.FindAsync(id);
        if (product == null)
        {
            return null;
        }

        product.IsActive = false;
        await _db.SaveChangesAsync();
        return product;
    }

    /// <inheritdoc />
    public Task<List<PriceEntity>> GetPricesByProductAsync(Guid productId)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        }

        return _db.Prices
            .AsNoTracking()
            .Include(p => p.Product)
            .Where(p => p.ProductId == productId)
            .OrderBy(p => p.Amount)
            .ToListAsync();
    }

    /// <inheritdoc />
    public Task<PriceEntity?> GetPriceByIdAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Price ID cannot be empty.", nameof(id));
        }

        return _db.Prices
            .AsNoTracking()
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <inheritdoc />
    public async Task<PriceEntity> CreatePriceAsync(Guid productId, string name, string billingPeriod, long amount, string currency, bool isActive)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Price name cannot be null or whitespace.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(billingPeriod))
        {
            throw new ArgumentException("Billing period cannot be null or whitespace.", nameof(billingPeriod));
        }

        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be null or whitespace.", nameof(currency));
        }

        var productExists = await _db.Products.AnyAsync(p => p.Id == productId);
        if (!productExists)
        {
            throw new InvalidOperationException("Product not found.");
        }

        var price = new PriceEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Name = name.Trim(),
            BillingPeriod = billingPeriod.Trim().ToLowerInvariant(),
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            IsActive = isActive,
            CreatedTime = DateTimeOffset.UtcNow
        };

        _db.Prices.Add(price);
        await _db.SaveChangesAsync();
        return price;
    }

    /// <inheritdoc />
    public async Task<PriceEntity?> UpdatePriceAsync(Guid id, string? name, long? amount, bool? isActive)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Price ID cannot be empty.", nameof(id));
        }

        if (amount.HasValue && amount.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
        }

        var price = await _db.Prices.FindAsync(id);
        if (price == null)
        {
            return null;
        }

        if (name != null)
            price.Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Price name cannot be empty when provided.", nameof(name)) : name.Trim();
        if (amount.HasValue)
            price.Amount = amount.Value;
        if (isActive.HasValue)
            price.IsActive = isActive.Value;

        await _db.SaveChangesAsync();
        return price;
    }

    /// <inheritdoc />
    public async Task<PriceEntity?> SoftDeletePriceAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Price ID cannot be empty.", nameof(id));
        }

        var price = await _db.Prices.FindAsync(id);
        if (price == null)
        {
            return null;
        }

        price.IsActive = false;
        await _db.SaveChangesAsync();
        return price;
    }

    /// <inheritdoc />
    public Task<List<ProductEntity>> GetActiveProductsAsync()
    {
        return _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<ProductEntity?> GetActiveProductDetailsAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(id));
        }

        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (product == null)
        {
            return null;
        }

        product.Prices = product.Prices.Where(pr => pr.IsActive).ToList();
        return product;
    }

    /// <inheritdoc />
    public Task<PriceEntity?> GetActivePriceWithProductAsync(Guid priceId)
    {
        if (priceId == Guid.Empty)
        {
            throw new ArgumentException("Price ID cannot be empty.", nameof(priceId));
        }

        return _db.Prices
            .AsNoTracking()
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == priceId && p.IsActive);
    }
}
