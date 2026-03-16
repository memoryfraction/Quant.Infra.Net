using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;

namespace Saas.Infra.Services.Product;

public interface IProductApplicationService
{
    Task<List<ProductEntity>> GetProductsAsync(bool activeOnly, bool isAdmin);
    Task<ProductEntity?> GetProductByIdAsync(Guid id);
    Task<ProductEntity> CreateProductAsync(string code, string name, string? description, bool isActive, string? metadata);
    Task<ProductEntity?> UpdateProductAsync(Guid id, string? name, string? description, bool? isActive, string? metadata);
    Task<ProductEntity?> SoftDeleteProductAsync(Guid id);
    Task<List<PriceEntity>> GetPricesByProductAsync(Guid productId);
    Task<PriceEntity?> GetPriceByIdAsync(Guid id);
    Task<PriceEntity> CreatePriceAsync(Guid productId, string name, string billingPeriod, long amount, string currency, bool isActive);
    Task<PriceEntity?> UpdatePriceAsync(Guid id, string? name, long? amount, bool? isActive);
    Task<PriceEntity?> SoftDeletePriceAsync(Guid id);
    Task<List<ProductEntity>> GetActiveProductsAsync();
    Task<ProductEntity?> GetActiveProductDetailsAsync(Guid id);
    Task<PriceEntity?> GetActivePriceWithProductAsync(Guid priceId);
}

public class ProductApplicationService : IProductApplicationService
{
    private readonly ApplicationDbContext _db;

    public ProductApplicationService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

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

    public Task<ProductEntity?> GetProductByIdAsync(Guid id)
    {
        return _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<ProductEntity> CreateProductAsync(string code, string name, string? description, bool isActive, string? metadata)
    {
        var normalizedCode = code.Trim();
        var exists = await _db.Products.AnyAsync(p => p.Code == normalizedCode);
        if (exists)
        {
            throw new InvalidOperationException("Product code already exists");
        }

        var product = new ProductEntity
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            Name = name,
            Description = description,
            IsActive = isActive,
            Metadata = metadata,
            CreatedTime = DateTimeOffset.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<ProductEntity?> UpdateProductAsync(Guid id, string? name, string? description, bool? isActive, string? metadata)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
        {
            return null;
        }

        if (name != null)
            product.Name = name;
        if (description != null)
            product.Description = description;
        if (isActive.HasValue)
            product.IsActive = isActive.Value;
        if (metadata != null)
            product.Metadata = metadata;

        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<ProductEntity?> SoftDeleteProductAsync(Guid id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null)
        {
            return null;
        }

        product.IsActive = false;
        await _db.SaveChangesAsync();
        return product;
    }

    public Task<List<PriceEntity>> GetPricesByProductAsync(Guid productId)
    {
        return _db.Prices
            .AsNoTracking()
            .Include(p => p.Product)
            .Where(p => p.ProductId == productId)
            .OrderBy(p => p.Amount)
            .ToListAsync();
    }

    public Task<PriceEntity?> GetPriceByIdAsync(Guid id)
    {
        return _db.Prices
            .AsNoTracking()
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PriceEntity> CreatePriceAsync(Guid productId, string name, string billingPeriod, long amount, string currency, bool isActive)
    {
        var productExists = await _db.Products.AnyAsync(p => p.Id == productId);
        if (!productExists)
        {
            throw new InvalidOperationException("Product not found");
        }

        var price = new PriceEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Name = name,
            BillingPeriod = billingPeriod.ToLower(),
            Amount = amount,
            Currency = currency,
            IsActive = isActive,
            CreatedTime = DateTimeOffset.UtcNow
        };

        _db.Prices.Add(price);
        await _db.SaveChangesAsync();
        return price;
    }

    public async Task<PriceEntity?> UpdatePriceAsync(Guid id, string? name, long? amount, bool? isActive)
    {
        var price = await _db.Prices.FindAsync(id);
        if (price == null)
        {
            return null;
        }

        if (name != null)
            price.Name = name;
        if (amount.HasValue)
            price.Amount = amount.Value;
        if (isActive.HasValue)
            price.IsActive = isActive.Value;

        await _db.SaveChangesAsync();
        return price;
    }

    public async Task<PriceEntity?> SoftDeletePriceAsync(Guid id)
    {
        var price = await _db.Prices.FindAsync(id);
        if (price == null)
        {
            return null;
        }

        price.IsActive = false;
        await _db.SaveChangesAsync();
        return price;
    }

    public Task<List<ProductEntity>> GetActiveProductsAsync()
    {
        return _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedTime)
            .ToListAsync();
    }

    public async Task<ProductEntity?> GetActiveProductDetailsAsync(Guid id)
    {
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

    public Task<PriceEntity?> GetActivePriceWithProductAsync(Guid priceId)
    {
        return _db.Prices
            .AsNoTracking()
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == priceId && p.IsActive);
    }
}
