using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Saas.Infra.MVC.Models.Requests;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.MVC.Security;
using Saas.Infra.Services.Product;
using Serilog.Events;

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
        private readonly IProductApplicationService _productApplicationService;

        /// <summary>
        /// 初始化<see cref="PricesController"/>的新实例。
        /// Initializes a new instance of the <see cref="PricesController"/> class.
        /// </summary>
        /// <param name="productApplicationService">产品应用服务。 / Product application service.</param>
        /// <exception cref="ArgumentNullException">当productApplicationService为null时抛出。 / Thrown when productApplicationService is null.</exception>
        public PricesController(IProductApplicationService productApplicationService)
        {
            _productApplicationService = productApplicationService ?? throw new ArgumentNullException(nameof(productApplicationService));
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
                var prices = await _productApplicationService.GetPricesByProductAsync(productId);
                var result = prices.Select(MapPrice).ToList();
                UtilityService.LogAndWriteLine(LogEventLevel.Information, "Retrieved {Count} prices for product {ProductId}", result.Count, productId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Error, "Error retrieving prices for product {ProductId}", productId);
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
                var price = await _productApplicationService.GetPriceByIdAsync(id);
                if (price == null)
                {
                    UtilityService.LogAndWriteLine(LogEventLevel.Warning, "Price {Id} not found", id);
                    return NotFound(new { message = "Price not found" });
                }

                return Ok(MapPrice(price));
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Error, "Error retrieving price {Id}", id);
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
                var price = await _productApplicationService.CreatePriceAsync(
                    request.ProductId,
                    request.Name,
                    request.BillingPeriod,
                    request.Amount,
                    request.Currency,
                    request.IsActive);

                var dto = MapPrice(price);
                UtilityService.LogAndWriteLine(LogEventLevel.Information, "Price created for product {ProductId} by {User}", request.ProductId, User.Identity?.Name ?? "unknown");
                return CreatedAtAction(nameof(GetPrice), new { id = price.Id }, dto);
            }
            catch (InvalidOperationException ex) when (ex.Message == "Product not found.")
            {
                UtilityService.LogAndWriteLine(LogEventLevel.Warning, "Product {ProductId} not found when creating price", request.ProductId);
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Error, "Error creating price for product {ProductId}", request.ProductId);
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
                var price = await _productApplicationService.UpdatePriceAsync(id, request.Name, request.Amount, request.IsActive);
                if (price == null)
                {
                    UtilityService.LogAndWriteLine(LogEventLevel.Warning, "Price {Id} not found for update", id);
                    return NotFound(new { message = "Price not found" });
                }

                UtilityService.LogAndWriteLine(LogEventLevel.Information, "Price {Id} updated by {User}", id, User.Identity?.Name ?? "unknown");
                return Ok(MapPrice(price));
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Error, "Error updating price {Id}", id);
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
                var price = await _productApplicationService.SoftDeletePriceAsync(id);
                if (price == null)
                {
                    UtilityService.LogAndWriteLine(LogEventLevel.Warning, "Price {Id} not found for deletion", id);
                    return NotFound(new { message = "Price not found" });
                }

                UtilityService.LogAndWriteLine(LogEventLevel.Information, "Price {Id} soft-deleted by {User}", id, User.Identity?.Name ?? "unknown");
                return Ok(new { message = "Price deleted successfully" });
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Error, "Error deleting price {Id}", id);
                return StatusCode(500, new { message = "Failed to delete price" });
            }
        }

        private static PriceDto MapPrice(PriceEntity price) => new()
        {
            Id = price.Id,
            ProductId = price.ProductId,
            ProductCode = price.Product?.Code,
            ProductName = price.Product?.Name,
            Name = price.Name,
            BillingPeriod = price.BillingPeriod,
            Amount = price.Amount,
            Currency = price.Currency,
            IsActive = price.IsActive,
            CreatedTime = price.CreatedTime
        };
    }
}
