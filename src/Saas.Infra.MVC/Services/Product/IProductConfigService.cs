namespace Saas.Infra.MVC.Services.Product;

/// <summary>
/// Manages available products and their URLs
/// </summary>
public interface IProductConfigService
{
    /// <summary>
    /// Gets all available products for the user
    /// </summary>
    Task<List<ProductInfo>> GetAvailableProductsAsync(string userId);

    /// <summary>
    /// Gets a specific product by ID
    /// </summary>
    Task<ProductInfo?> GetProductAsync(string productId);
}

/// <summary>
/// Information about a product
/// </summary>
public class ProductInfo
{
    /// <summary>
    /// Product ID (e.g., "cryptocycleai")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Product name (e.g., "CryptoCycleAI")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Product URL (e.g., "/dashboard")
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional product icon URL
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Optional product description
    /// </summary>
    public string? Description { get; set; }
}
