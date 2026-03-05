using Microsoft.Extensions.Configuration;
using Saas.Infra.MVC.Services.Product;

namespace Saas.Infra.MVC.Tests.Services;

/// <summary>
/// Unit tests for ProductConfigService
/// </summary>
public class ProductConfigServiceTests
{
    private readonly IProductConfigService _service;

    public ProductConfigServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Products:Available:0:Id", "cryptocycleai" },
                { "Products:Available:0:Name", "CryptoCycleAI" },
                { "Products:Available:0:Url", "/dashboard" },
                { "Products:Available:0:IconUrl", "/images/cryptocycleai-icon.png" },
                { "Products:Available:0:Description", "AI-powered cryptocurrency analysis" },
                { "Products:Available:1:Id", "analytics" },
                { "Products:Available:1:Name", "Analytics" },
                { "Products:Available:1:Url", "/analytics" },
                { "Products:Available:1:IconUrl", "/images/analytics-icon.png" },
                { "Products:Available:1:Description", "Advanced analytics platform" }
            })
            .Build();

        _service = new ProductConfigService(config);
    }

    /// <summary>
    /// Test: GetAvailableProductsAsync returns all products
    /// </summary>
    [Fact]
    public async Task GetAvailableProductsAsync_ReturnsAllProducts()
    {
        var products = await _service.GetAvailableProductsAsync("user123");
        
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
        var product = await _service.GetProductAsync("cryptocycleai");
        
        Assert.NotNull(product);
        Assert.Equal("cryptocycleai", product.Id);
        Assert.Equal("CryptoCycleAI", product.Name);
        Assert.Equal("/dashboard", product.Url);
    }

    /// <summary>
    /// Test: GetProductAsync is case-insensitive
    /// </summary>
    [Fact]
    public async Task GetProductAsync_IsCaseInsensitive()
    {
        var product = await _service.GetProductAsync("CRYPTOCYCLEAI");
        
        Assert.NotNull(product);
        Assert.Equal("cryptocycleai", product.Id);
    }

    /// <summary>
    /// Test: GetProductAsync returns null for missing product
    /// </summary>
    [Fact]
    public async Task GetProductAsync_WithInvalidId_ReturnsNull()
    {
        var product = await _service.GetProductAsync("nonexistent");
        
        Assert.Null(product);
    }

    /// <summary>
    /// Test: GetAvailableProductsAsync returns empty list when no products configured
    /// </summary>
    [Fact]
    public async Task GetAvailableProductsAsync_WithNoProducts_ReturnsEmptyList()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var service = new ProductConfigService(config);
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
        var product = await _service.GetProductAsync("cryptocycleai");
        
        Assert.NotNull(product);
        Assert.Equal("cryptocycleai", product.Id);
        Assert.Equal("CryptoCycleAI", product.Name);
        Assert.Equal("/dashboard", product.Url);
        Assert.Equal("/images/cryptocycleai-icon.png", product.IconUrl);
        Assert.Equal("AI-powered cryptocurrency analysis", product.Description);
    }
}
