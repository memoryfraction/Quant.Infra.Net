using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;
using System.Data.Common;

namespace Saas.Infra.MVC.Services.Product;

/// <summary>
/// 产品配置服务，从数据库读取可用产品信息。使用安全的原始查询以避免可选列缺失时的失败。
/// Product configuration service that reads available products from the database using safe raw queries to avoid failures when optional columns are missing.
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
    /// 获取用户所有可用的产品列表。读取最小列集（Id, Name, Url, Description）并映射到ProductInfo。避免数据库中不存在可选列（如IconUrl）时的错误。
    /// Gets all available products for the user. Reads a minimal set of columns and maps to ProductInfo to avoid errors when optional columns are missing.
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
            DbConnection? conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"Code\", \"Name\", \"Description\", \"AllowedPaymentGateways\", \"Metadata\", \"IsActive\" FROM \"Products\" WHERE coalesce(\"IsActive\", true) = true";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var code = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var desc = reader.FieldCount > 2 && !reader.IsDBNull(2) ? reader.GetString(2) : null;

                string[]? gateways = null;
                if (reader.FieldCount > 3 && !reader.IsDBNull(3))
                {
                    try { gateways = reader.GetFieldValue<string[]>(3); } catch { gateways = null; }
                }

                string? metadata = null;
                if (reader.FieldCount > 4 && !reader.IsDBNull(4))
                {
                    try { metadata = reader.GetFieldValue<string>(4); } catch { metadata = null; }
                }

                result.Add(new ProductInfo
                {
                    Id = code,
                    Name = name,
                    Description = desc,
                    AllowedPaymentGateways = gateways,
                    Metadata = metadata
                });
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to load available products for user {UserId}", userId);
            return new List<ProductInfo>();
        }

        return result;
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
            DbConnection? conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"Id\", \"Name\", \"Url\", \"Description\" FROM \"Products\" WHERE \"Id\" = @id";
            var param = cmd.CreateParameter();
            param.ParameterName = "@id";
            param.Value = productId;
            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var url = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var desc = reader.FieldCount > 3 && !reader.IsDBNull(3) ? reader.GetString(3) : null;

                return new ProductInfo
                {
                    Id = id,
                    Name = name,
                    Url = url,
                    Description = desc
                };
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to load product {ProductId}", productId);
            return null;
        }

        return null;
    }
}
