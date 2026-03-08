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
    /// 产品管理API控制器（管理员）。
    /// Product management API controller (Admin).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// 初始化<see cref="ProductsController"/>的新实例。
        /// Initializes a new instance of the <see cref="ProductsController"/> class.
        /// </summary>
        /// <param name="db">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当db为null时抛出。 / Thrown when db is null.</exception>
        public ProductsController(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 获取所有产品（管理员查看全部，用户仅查看激活的）。
        /// Gets all products (Admin sees all, users see only active ones).
        /// </summary>
        /// <param name="activeOnly">是否仅查看激活的产品。 / Whether to show only active products.</param>
        /// <returns>产品列表。 / List of products.</returns>
        [HttpGet]
        [AuthorizeRole(UserRole.User)]
        public async Task<IActionResult> GetProducts([FromQuery] bool activeOnly = false)
        {
            try
            {
                var query = _db.Products.AsNoTracking();

                // 用户只能查看激活的产品
                if (activeOnly || !User.IsInRole("ADMIN") && !User.IsInRole("SUPER_ADMIN"))
                {
                    query = query.Where(p => p.IsActive);
                }

                var products = await query
                    .OrderByDescending(p => p.CreatedTime)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        Description = p.Description,
                        IsActive = p.IsActive,
                        Metadata = p.Metadata,
                        CreatedTime = p.CreatedTime
                    })
                    .ToListAsync();

                Log.Information("Retrieved {Count} products (activeOnly={ActiveOnly})", products.Count, activeOnly);
                return Ok(products);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving products");
                return StatusCode(500, new { message = "Failed to retrieve products" });
            }
        }

        /// <summary>
        /// 根据ID获取单个产品。
        /// Gets a single product by ID.
        /// </summary>
        /// <param name="id">产品ID。 / Product ID.</param>
        /// <returns>产品详情。 / Product details.</returns>
        [HttpGet("{id}")]
        [AuthorizeRole(UserRole.User)]
        public async Task<IActionResult> GetProduct(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid product ID" });

            try
            {
                var product = await _db.Products
                    .AsNoTracking()
                    .Where(p => p.Id == id)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        Description = p.Description,
                        IsActive = p.IsActive,
                        Metadata = p.Metadata,
                        CreatedTime = p.CreatedTime
                    })
                    .FirstOrDefaultAsync();

                if (product == null)
                {
                    Log.Warning("Product {Id} not found", id);
                    return NotFound(new { message = "Product not found" });
                }

                return Ok(product);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving product {Id}", id);
                return StatusCode(500, new { message = "Failed to retrieve product" });
            }
        }

        /// <summary>
        /// 创建新产品（仅管理员）。
        /// Creates a new product (Admin only).
        /// </summary>
        /// <param name="request">创建产品请求。 / Create product request.</param>
        /// <returns>创建的产品。 / Created product.</returns>
        [HttpPost]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Request cannot be null" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // 检查代码是否已存在
                var exists = await _db.Products.AnyAsync(p => p.Code == request.Code);
                if (exists)
                {
                    Log.Warning("Product code {Code} already exists", request.Code);
                    return Conflict(new { message = "Product code already exists" });
                }

                var product = new ProductEntity
                {
                    Id = Guid.NewGuid(),
                    Code = request.Code,
                    Name = request.Name,
                    Description = request.Description,
                    IsActive = request.IsActive,
                    Metadata = request.Metadata,
                    CreatedTime = DateTimeOffset.UtcNow
                };

                _db.Products.Add(product);
                await _db.SaveChangesAsync();

                var dto = new ProductDto
                {
                    Id = product.Id,
                    Code = product.Code,
                    Name = product.Name,
                    Description = product.Description,
                    IsActive = product.IsActive,
                    Metadata = product.Metadata,
                    CreatedTime = product.CreatedTime
                };

                Log.Information("Product created: {Code} by {User}", request.Code, User.Identity?.Name);
                return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, dto);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating product: {Code}", request.Code);
                return StatusCode(500, new { message = "Failed to create product" });
            }
        }

        /// <summary>
        /// 更新产品（仅管理员）。
        /// Updates a product (Admin only).
        /// </summary>
        /// <param name="id">产品ID。 / Product ID.</param>
        /// <param name="request">更新产品请求。 / Update product request.</param>
        /// <returns>更新后的产品。 / Updated product.</returns>
        [HttpPut("{id}")]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid product ID" });

            if (request == null)
                return BadRequest(new { message = "Request cannot be null" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var product = await _db.Products.FindAsync(id);
                if (product == null)
                {
                    Log.Warning("Product {Id} not found for update", id);
                    return NotFound(new { message = "Product not found" });
                }

                // 只更新非null的字段
                if (request.Name != null)
                    product.Name = request.Name;

                if (request.Description != null)
                    product.Description = request.Description;

                if (request.IsActive.HasValue)
                    product.IsActive = request.IsActive.Value;

                if (request.Metadata != null)
                    product.Metadata = request.Metadata;

                await _db.SaveChangesAsync();

                var dto = new ProductDto
                {
                    Id = product.Id,
                    Code = product.Code,
                    Name = product.Name,
                    Description = product.Description,
                    IsActive = product.IsActive,
                    Metadata = product.Metadata,
                    CreatedTime = product.CreatedTime
                };

                Log.Information("Product {Id} updated by {User}", id, User.Identity?.Name);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating product {Id}", id);
                return StatusCode(500, new { message = "Failed to update product" });
            }
        }

        /// <summary>
        /// 删除产品（软删除，仅管理员）。
        /// Deletes a product (soft delete, Admin only).
        /// </summary>
        /// <param name="id">产品ID。 / Product ID.</param>
        /// <returns>删除结果。 / Deletion result.</returns>
        [HttpDelete("{id}")]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid product ID" });

            try
            {
                var product = await _db.Products.FindAsync(id);
                if (product == null)
                {
                    Log.Warning("Product {Id} not found for deletion", id);
                    return NotFound(new { message = "Product not found" });
                }

                // 软删除：设置为不激活
                product.IsActive = false;
                await _db.SaveChangesAsync();

                Log.Information("Product {Id} ({Code}) soft-deleted by {User}", id, product.Code, User.Identity?.Name);
                return Ok(new { message = "Product deleted successfully" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting product {Id}", id);
                return StatusCode(500, new { message = "Failed to delete product" });
            }
        }
    }
}
