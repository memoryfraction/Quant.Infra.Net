using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;
using System.Data.Common;

namespace Saas.Infra.MVC.Services.Product;

/// <summary>
/// Product service that reads available products from the database.
/// Uses a safe raw query to avoid failures when optional columns are missing in the DB schema.
/// </summary>
public class ProductConfigService : IProductConfigService
{
    private readonly ApplicationDbContext _db;

    public ProductConfigService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// Gets all available products for the user.
    /// Reads a minimal set of columns (Id, Name, Url, Description) and maps to ProductInfo.
    /// This avoids errors when optional columns (e.g. IconUrl) are not present in the database.
    /// </summary>
    public async Task<List<ProductInfo>> GetAvailableProductsAsync(string userId)
    {
        var result = new List<ProductInfo>();

        try
        {
            DbConnection? conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"Id\", \"Name\", \"Url\", \"Description\" FROM \"Products\"";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                var url = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                var desc = reader.FieldCount > 3 && !reader.IsDBNull(3) ? reader.GetString(3) : null;

                result.Add(new ProductInfo
                {
                    Id = id,
                    Name = name,
                    Url = url,
                    Description = desc
                });
            }
        }
        catch (Exception)
        {
            // Swallow and return empty list — product loading is non-critical for login flow
            return new List<ProductInfo>();
        }

        return result;
    }

    /// <summary>
    /// Gets a specific product by ID (case-insensitive)
    /// </summary>
    public async Task<ProductInfo?> GetProductAsync(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId)) return null;

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
        catch (Exception)
        {
            return null;
        }

        return null;
    }
}
