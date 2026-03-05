namespace Saas.Infra.MVC.Services.Product;

/// <summary>
/// Manages available products and their URLs
/// </summary>
public class ProductConfigService : IProductConfigService
{
    private readonly IConfiguration _configuration;
    private readonly List<ProductInfo> _products;

    public ProductConfigService(IConfiguration configuration)
    {
        _configuration = configuration;
        _products = _configuration.GetSection("Products:Available")
            .Get<List<ProductInfo>>() ?? new List<ProductInfo>();
    }

    /// <summary>
    /// Gets all available products for the user
    /// </summary>
    public async Task<List<ProductInfo>> GetAvailableProductsAsync(string userId)
    {
        // In future, could filter by user permissions
        return await Task.FromResult(_products);
    }

    /// <summary>
    /// Gets a specific product by ID (case-insensitive)
    /// </summary>
    public async Task<ProductInfo?> GetProductAsync(string productId)
    {
        return await Task.FromResult(
            _products.FirstOrDefault(p => p.Id.Equals(productId, StringComparison.OrdinalIgnoreCase))
        );
    }
}
