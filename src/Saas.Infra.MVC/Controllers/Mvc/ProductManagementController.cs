using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Saas.Infra.MVC.Security;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Mvc
{
    /// <summary>
    /// 产品管理MVC控制器（管理员端）。
    /// Product management MVC controller (Admin side).
    /// </summary>
    [NonController]
    [AuthorizeRole(UserRole.Admin)]
    public class ProductManagementController : Controller
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// 初始化<see cref="ProductManagementController"/>的新实例。
        /// Initializes a new instance of the <see cref="ProductManagementController"/> class.
        /// </summary>
        /// <param name="db">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当db为null时抛出。 / Thrown when db is null.</exception>
        public ProductManagementController(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 产品管理列表页面。
        /// Product management list page.
        /// </summary>
        /// <returns>产品管理视图。 / Product management view.</returns>
        [HttpGet]
        [Route("product-management")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _db.Products
                    .AsNoTracking()
                    .OrderByDescending(p => p.CreatedTime)
                    .ToListAsync();

                Log.Information("Product management page accessed by {User}, {Count} products loaded",
                    User.Identity?.Name, products.Count);

                return View(products);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading product management page");
                return View("Error");
            }
        }

        /// <summary>
        /// 创建产品页面。
        /// Create product page.
        /// </summary>
        /// <returns>创建产品视图。 / Create product view.</returns>
        [HttpGet]
        [Route("product-management/create")]
        public IActionResult Create()
        {
            Log.Information("Create product page accessed by {User}", User.Identity?.Name);
            return View();
        }

        /// <summary>
        /// 编辑产品页面。
        /// Edit product page.
        /// </summary>
        /// <param name="id">产品ID。 / Product ID.</param>
        /// <returns>编辑产品视图。 / Edit product view.</returns>
        [HttpGet]
        [Route("product-management/edit/{id}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            if (id == Guid.Empty)
            {
                Log.Warning("Invalid product ID for edit");
                return RedirectToAction("Index");
            }

            try
            {
                var product = await _db.Products.FindAsync(id);
                if (product == null)
                {
                    Log.Warning("Product {Id} not found for edit", id);
                    return NotFound();
                }

                Log.Information("Edit product page accessed for {ProductCode} by {User}",
                    product.Code, User.Identity?.Name);

                return View(product);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading edit product page for {Id}", id);
                return View("Error");
            }
        }

        /// <summary>
        /// 价格管理页面。
        /// Price management page.
        /// </summary>
        /// <param name="id">产品ID。 / Product ID.</param>
        /// <returns>价格管理视图。 / Price management view.</returns>
        [HttpGet]
        [Route("product-management/prices/{id}")]
        public async Task<IActionResult> Prices(Guid id)
        {
            if (id == Guid.Empty)
            {
                Log.Warning("Invalid product ID for price management");
                return RedirectToAction("Index");
            }

            try
            {
                var product = await _db.Products
                    .AsNoTracking()
                    .Include(p => p.Prices)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    Log.Warning("Product {Id} not found for price management", id);
                    return NotFound();
                }

                Log.Information("Price management page accessed for {ProductCode} by {User}",
                    product.Code, User.Identity?.Name);

                return View(product);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading price management page for {Id}", id);
                return View("Error");
            }
        }
    }
}
