using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;
using Saas.Infra.MVC.Services.Product;

namespace Saas.Infra.MVC.Tests.Services;

/// <summary>
/// Unit tests for ProductConfigService
/// </summary>
public class ProductConfigServiceTests
{
    private static ProductConfigService CreateServiceWithProducts(IEnumerable<ProductEntity>? products = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        if (products is not null)
        {
            context.Products.AddRange(products);
            context.SaveChanges();
        }

        return new ProductConfigService(context);
    }

    /// <summary>
    /// Test: GetAvailableProductsAsync returns all products
    /// </summary>
    [Fact]
    public async Task GetAvailableProductsAsync_ReturnsAllProducts()
    {
        var service = CreateServiceWithProducts(new[]
        {
            new ProductEntity { Id = Guid.NewGuid(), Code = "cryptocycleai", Name = "CryptoCycleAI", Description = "AI-powered cryptocurrency analysis", IsActive = true, CreatedTime = DateTimeOffset.UtcNow },
            new ProductEntity { Id = Guid.NewGuid(), Code = "analytics", Name = "Analytics", Description = "Advanced analytics platform", IsActive = true, CreatedTime = DateTimeOffset.UtcNow },
            new ProductEntity { Id = Guid.NewGuid(), Code = "inactive", Name = "Inactive", Description = "Inactive product", IsActive = false, CreatedTime = DateTimeOffset.UtcNow }
        });

        var products = await service.GetAvailableProductsAsync("user123");
        
        Assert.NotNull(products);
        Assert.Equal(2, products.Count);
        Assert.Contains(products, p => p.Id == "cryptocycleai");
        Assert.Contains(products, p => p.Id == "analytics");
    }

    /// <summary>
    /// Test: GetProductAsync retrieves product by ID
    /// </summary>
    [Fact]
    public async Task GetProductAsync_WithValidId_ReturnsProduct()
    {
        var service = CreateServiceWithProducts(new[]
        {
            new ProductEntity { Id = Guid.NewGuid(), Code = "cryptocycleai", Name = "CryptoCycleAI", Description = "AI-powered cryptocurrency analysis", IsActive = true, CreatedTime = DateTimeOffset.UtcNow }
        });

        var product = await service.GetProductAsync("cryptocycleai");
        
        Assert.NotNull(product);
        Assert.Equal("cryptocycleai", product.Id);
        Assert.Equal("CryptoCycleAI", product.Name);
        Assert.Equal("AI-powered cryptocurrency analysis", product.Description);
    }

    /// <summary>
    /// Test: GetProductAsync is case-insensitive
    /// </summary>
    [Fact]
    public async Task GetProductAsync_IsCaseInsensitive()
    {
        var service = CreateServiceWithProducts(new[]
        {
            new ProductEntity { Id = Guid.NewGuid(), Code = "cryptocycleai", Name = "CryptoCycleAI", Description = "AI-powered cryptocurrency analysis", IsActive = true, CreatedTime = DateTimeOffset.UtcNow }
        });

        var product = await service.GetProductAsync("CRYPTOCYCLEAI");
        
        Assert.NotNull(product);
        Assert.Equal("cryptocycleai", product.Id);
    }

    /// <summary>
    /// Test: GetProductAsync returns null for missing product
    /// </summary>
    [Fact]
    public async Task GetProductAsync_WithInvalidId_ReturnsNull()
    {
        var service = CreateServiceWithProducts(new[]
        {
            new ProductEntity { Id = Guid.NewGuid(), Code = "cryptocycleai", Name = "CryptoCycleAI", Description = "AI-powered cryptocurrency analysis", IsActive = true, CreatedTime = DateTimeOffset.UtcNow }
        });

        var product = await service.GetProductAsync("nonexistent");
        
        Assert.Null(product);
    }

    /// <summary>
    /// Test: GetAvailableProductsAsync returns empty list when no products configured
    /// </summary>
    [Fact]
    public async Task GetAvailableProductsAsync_WithNoProducts_ReturnsEmptyList()
    {
        var service = CreateServiceWithProducts();
        var products = await service.GetAvailableProductsAsync("user123");
        
        Assert.NotNull(products);
        Assert.Empty(products);
    }

    /// <summary>
    /// Test: Product properties are correctly loaded
    /// </summary>
    [Fact]
    public async Task GetProductAsync_LoadsAllProperties()
    {
        var service = CreateServiceWithProducts(new[]
        {
            new ProductEntity
            {
                Id = Guid.NewGuid(),
                Code = "cryptocycleai",
                Name = "CryptoCycleAI",
                Description = "AI-powered cryptocurrency analysis",
                Metadata = "{\"icon\":\"/images/cryptocycleai-icon.png\"}",
                IsActive = true,
                CreatedTime = DateTimeOffset.UtcNow
            }
        });

        var product = await service.GetProductAsync("cryptocycleai");
        
        Assert.NotNull(product);
        Assert.Equal("cryptocycleai", product.Id);
        Assert.Equal("CryptoCycleAI", product.Name);
        Assert.Equal("AI-powered cryptocurrency analysis", product.Description);
        Assert.Equal("{\"icon\":\"/images/cryptocycleai-icon.png\"}", product.Metadata);
    }
}
