using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Services.Product;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Mvc
{
    /// <summary>
    /// 产品展示MVC控制器（用户端）。
    /// Product display MVC controller (User side).
    /// </summary>
    [NonController]
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly IProductApplicationService _productApplicationService;

        /// <summary>
        /// 初始化<see cref="ProductsController"/>的新实例。
        /// Initializes a new instance of the <see cref="ProductsController"/> class.
        /// </summary>
        /// <param name="productApplicationService">产品应用服务。 / Product application service.</param>
        /// <exception cref="ArgumentNullException">当productApplicationService为null时抛出。 / Thrown when productApplicationService is null.</exception>
        public ProductsController(IProductApplicationService productApplicationService)
        {
            _productApplicationService = productApplicationService ?? throw new ArgumentNullException(nameof(productApplicationService));
        }

        /// <summary>
        /// 产品列表页面（显示所有激活的产品）。
        /// Product list page (displays all active products).
        /// </summary>
        /// <returns>产品列表视图。 / Product list view.</returns>
        [HttpGet]
        [Route("products")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _productApplicationService.GetActiveProductsAsync();
                Log.Information("Products page accessed, {Count} products displayed", products.Count);
                return View(products);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading products page");
                return View("Error");
            }
        }

        /// <summary>
        /// 产品详情页面（显示产品信息和价格方案）。
        /// Product details page (displays product info and pricing plans).
        /// </summary>
        /// <param name="id">产品ID。 / Product ID.</param>
        /// <returns>产品详情视图。 / Product details view.</returns>
        [HttpGet]
        [Route("products/{id}")]
        public async Task<IActionResult> Details(Guid id)
        {
            if (id == Guid.Empty)
            {
                Log.Warning("Invalid product ID: {Id}", id);
                return NotFound();
            }

            try
            {
                var product = await _productApplicationService.GetActiveProductDetailsAsync(id);
                if (product == null)
                {
                    Log.Warning("Product {Id} not found or not active", id);
                    return NotFound();
                }

                Log.Information("Product details page accessed for {ProductCode}", product.Code);
                return View(product);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading product details for {Id}", id);
                return View("Error");
            }
        }
    }
}
