namespace Saas.Infra.MVC.Services.Product;

/// <summary>
/// 管理可用产品及其URL的服务接口。
/// Interface for managing available products and their URLs.
/// </summary>
public interface IProductConfigService
{
    /// <summary>
    /// 获取用户所有可用的产品列表。
    /// Gets all available products for the user.
    /// </summary>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <returns>产品信息列表的任务。 / Task containing list of product information.</returns>
    Task<List<ProductInfo>> GetAvailableProductsAsync(string userId);

    /// <summary>
    /// 根据ID获取特定产品信息。
    /// Gets a specific product by ID.
    /// </summary>
    /// <param name="productId">产品标识。 / Product identifier.</param>
    /// <returns>产品信息的任务（如果未找到则返回null）。 / Task containing product information (null if not found).</returns>
    Task<ProductInfo?> GetProductAsync(string productId);
}

/// <summary>
/// 产品信息类。
/// Information about a product.
/// </summary>
public class ProductInfo
{
    /// <summary>
    /// 产品标识（例如："cryptocycleai"）。
    /// Product ID (e.g., "cryptocycleai").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 产品名称（例如："CryptoCycleAI"）。
    /// Product name (e.g., "CryptoCycleAI").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 产品URL（例如："/dashboard"）。
    /// Product URL (e.g., "/dashboard").
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 产品描述（可选）。
    /// Optional product description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 产品图标URL（可选，新架构中不持久化，可能由元数据提供）。
    /// Optional product icon URL (not persisted in new schema; may be provided by metadata).
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// 允许的支付网关数组（text[]）。
    /// Allowed payment gateways (text[]).
    /// </summary>
    public string[]? AllowedPaymentGateways { get; set; }

    /// <summary>
    /// 原始JSON元数据。
    /// Raw JSON metadata.
    /// </summary>
    public string? Metadata { get; set; }
}
