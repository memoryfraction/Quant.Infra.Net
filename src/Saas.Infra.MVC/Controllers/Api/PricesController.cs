using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Saas.Infra.MVC.Models.Requests;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.MVC.Security;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Api
{
    /// <summary>
    /// 价格管理API控制器（管理员）。
    /// Price management API controller (Admin).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PricesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// 初始化<see cref="PricesController"/>的新实例。
        /// Initializes a new instance of the <see cref="PricesController"/> class.
        /// </summary>
        /// <param name="db">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当db为null时抛出。 / Thrown when db is null.</exception>
        public PricesController(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 获取指定产品的所有价格。
        /// Gets all prices for a specific product.
        /// </summary>
        /// <param name="productId">产品ID。 / Product ID.</param>
        /// <returns>价格列表。 / List of prices.</returns>
        [HttpGet("product/{productId}")]
        [AuthorizeRole(UserRole.User)]
        public async Task<IActionResult> GetPricesByProduct(Guid productId)
        {
            if (productId == Guid.Empty)
                return BadRequest(new { message = "Invalid product ID" });

            try
            {
                var prices = await _db.Prices
                    .AsNoTracking()
                    .Include(p => p.Product)
                    .Where(p => p.ProductId == productId)
                    .OrderBy(p => p.Amount)
                    .Select(p => new PriceDto
                    {
                        Id = p.Id,
                        ProductId = p.ProductId,
                        ProductCode = p.Product != null ? p.Product.Code : null,
                        ProductName = p.Product != null ? p.Product.Name : null,
                        Name = p.Name,
                        BillingPeriod = p.BillingPeriod,
                        Amount = p.Amount,
                        Currency = p.Currency,
                        IsActive = p.IsActive,
                        CreatedTime = p.CreatedTime
                    })
                    .ToListAsync();

                Log.Information("Retrieved {Count} prices for product {ProductId}", prices.Count, productId);
                return Ok(prices);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving prices for product {ProductId}", productId);
                return StatusCode(500, new { message = "Failed to retrieve prices" });
            }
        }

        /// <summary>
        /// 根据ID获取单个价格。
        /// Gets a single price by ID.
        /// </summary>
        /// <param name="id">价格ID。 / Price ID.</param>
        /// <returns>价格详情。 / Price details.</returns>
        [HttpGet("{id}")]
        [AuthorizeRole(UserRole.User)]
        public async Task<IActionResult> GetPrice(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid price ID" });

            try
            {
                var price = await _db.Prices
                    .AsNoTracking()
                    .Include(p => p.Product)
                    .Where(p => p.Id == id)
                    .Select(p => new PriceDto
                    {
                        Id = p.Id,
                        ProductId = p.ProductId,
                        ProductCode = p.Product != null ? p.Product.Code : null,
                        ProductName = p.Product != null ? p.Product.Name : null,
                        Name = p.Name,
                        BillingPeriod = p.BillingPeriod,
                        Amount = p.Amount,
                        Currency = p.Currency,
                        IsActive = p.IsActive,
                        CreatedTime = p.CreatedTime
                    })
                    .FirstOrDefaultAsync();

                if (price == null)
                {
                    Log.Warning("Price {Id} not found", id);
                    return NotFound(new { message = "Price not found" });
                }

                return Ok(price);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving price {Id}", id);
                return StatusCode(500, new { message = "Failed to retrieve price" });
            }
        }

        /// <summary>
        /// 创建新价格（仅管理员）。
        /// Creates a new price (Admin only).
        /// </summary>
        /// <param name="request">创建价格请求。 / Create price request.</param>
        /// <returns>创建的价格。 / Created price.</returns>
        [HttpPost]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> CreatePrice([FromBody] CreatePriceRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Request cannot be null" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // 验证产品是否存在
                var productExists = await _db.Products.AnyAsync(p => p.Id == request.ProductId);
                if (!productExists)
                {
                    Log.Warning("Product {ProductId} not found when creating price", request.ProductId);
                    return NotFound(new { message = "Product not found" });
                }

                var price = new PriceEntity
                {
                    Id = Guid.NewGuid(),
                    ProductId = request.ProductId,
                    Name = request.Name,
                    BillingPeriod = request.BillingPeriod.ToLower(),
                    Amount = request.Amount,
                    Currency = request.Currency,
                    IsActive = request.IsActive,
                    CreatedTime = DateTimeOffset.UtcNow
                };

                _db.Prices.Add(price);
                await _db.SaveChangesAsync();

                var dto = new PriceDto
                {
                    Id = price.Id,
                    ProductId = price.ProductId,
                    Name = price.Name,
                    BillingPeriod = price.BillingPeriod,
                    Amount = price.Amount,
                    Currency = price.Currency,
                    IsActive = price.IsActive,
                    CreatedTime = price.CreatedTime
                };

                Log.Information("Price created for product {ProductId} by {User}", request.ProductId, User.Identity?.Name);
                return CreatedAtAction(nameof(GetPrice), new { id = price.Id }, dto);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating price for product {ProductId}", request.ProductId);
                return StatusCode(500, new { message = "Failed to create price" });
            }
        }

        /// <summary>
        /// 更新价格（仅管理员）。
        /// Updates a price (Admin only).
        /// </summary>
        /// <param name="id">价格ID。 / Price ID.</param>
        /// <param name="request">更新价格请求。 / Update price request.</param>
        /// <returns>更新后的价格。 / Updated price.</returns>
        [HttpPut("{id}")]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> UpdatePrice(Guid id, [FromBody] UpdatePriceRequest request)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid price ID" });

            if (request == null)
                return BadRequest(new { message = "Request cannot be null" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var price = await _db.Prices.FindAsync(id);
                if (price == null)
                {
                    Log.Warning("Price {Id} not found for update", id);
                    return NotFound(new { message = "Price not found" });
                }

                // 只更新非null的字段
                if (request.Name != null)
                    price.Name = request.Name;

                if (request.Amount.HasValue)
                    price.Amount = request.Amount.Value;

                if (request.IsActive.HasValue)
                    price.IsActive = request.IsActive.Value;

                await _db.SaveChangesAsync();

                var dto = new PriceDto
                {
                    Id = price.Id,
                    ProductId = price.ProductId,
                    Name = price.Name,
                    BillingPeriod = price.BillingPeriod,
                    Amount = price.Amount,
                    Currency = price.Currency,
                    IsActive = price.IsActive,
                    CreatedTime = price.CreatedTime
                };

                Log.Information("Price {Id} updated by {User}", id, User.Identity?.Name);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating price {Id}", id);
                return StatusCode(500, new { message = "Failed to update price" });
            }
        }

        /// <summary>
        /// 删除价格（软删除，仅管理员）。
        /// Deletes a price (soft delete, Admin only).
        /// </summary>
        /// <param name="id">价格ID。 / Price ID.</param>
        /// <returns>删除结果。 / Deletion result.</returns>
        [HttpDelete("{id}")]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> DeletePrice(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid price ID" });

            try
            {
                var price = await _db.Prices.FindAsync(id);
                if (price == null)
                {
                    Log.Warning("Price {Id} not found for deletion", id);
                    return NotFound(new { message = "Price not found" });
                }

                // 软删除：设置为不激活
                price.IsActive = false;
                await _db.SaveChangesAsync();

                Log.Information("Price {Id} soft-deleted by {User}", id, User.Identity?.Name);
                return Ok(new { message = "Price deleted successfully" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting price {Id}", id);
                return StatusCode(500, new { message = "Failed to delete price" });
            }
        }
    }
}
