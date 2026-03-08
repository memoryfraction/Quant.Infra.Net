using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Saas.Infra.MVC.Models;
using Saas.Infra.MVC.Security;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Mvc
{
    /// <summary>
    /// 管理后台MVC控制器。
    /// Admin MVC controller.
    /// </summary>
    [NonController]
    [AuthorizeRole(UserRole.Admin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// 初始化 <see cref="AdminController"/> 的新实例。
        /// Initializes a new instance of <see cref="AdminController"/>.
        /// </summary>
        /// <param name="db">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当 db 为空时抛出。 / Thrown when db is null.</exception>
        public AdminController(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 管理后台首页。
        /// Admin home page.
        /// </summary>
        /// <returns>管理后台首页视图。 / Admin home view.</returns>
        [HttpGet]
        [Route("admin")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var model = new AdminDashboardViewModel
                {
                    TotalProducts = await _db.Products.AsNoTracking().CountAsync(),
                    ActiveProducts = await _db.Products.AsNoTracking().CountAsync(p => p.IsActive),
                    TotalSubscriptions = await _db.Subscriptions.AsNoTracking().CountAsync(s => !s.IsDeleted),
                    ActiveSubscriptions = await _db.Subscriptions.AsNoTracking().CountAsync(s => !s.IsDeleted && s.Status == 1),
                    TotalTransactions = await _db.Transactions.AsNoTracking().CountAsync(),
                    SuccessfulTransactions = await _db.Transactions.AsNoTracking().CountAsync(t => t.Status == 1)
                };

                Log.Information("Admin dashboard accessed by {User}", User.Identity?.Name);
                return View(model);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading admin dashboard");
                return View("Error");
            }
        }

        /// <summary>
        /// 管理后台订阅列表。
        /// Admin subscription list page.
        /// </summary>
        /// <param name="keyword">查询关键字。 / Search keyword.</param>
        /// <param name="status">状态筛选。 / Status filter.</param>
        /// <returns>订阅列表视图。 / Subscription list view.</returns>
        [HttpGet]
        [Route("admin/subscriptions")]
        public async Task<IActionResult> Subscriptions([FromQuery] string? keyword, [FromQuery] short? status)
        {
            if (keyword != null && keyword.Length > 200)
            {
                throw new ArgumentException("keyword length cannot exceed 200", nameof(keyword));
            }

            try
            {
                var query = _db.Subscriptions
                    .AsNoTracking()
                    .Include(s => s.User)
                    .Include(s => s.Product)
                    .Include(s => s.Price)
                    .Where(s => !s.IsDeleted);

                if (status.HasValue)
                {
                    query = query.Where(s => s.Status == status.Value);
                }

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    var normalized = keyword.Trim().ToLowerInvariant();
                    query = query.Where(s =>
                        (s.User != null && ((s.User.UserName != null && s.User.UserName.ToLower().Contains(normalized)) ||
                                            (s.User.Email != null && s.User.Email.ToLower().Contains(normalized)))) ||
                        (s.Product != null && ((s.Product.Code != null && s.Product.Code.ToLower().Contains(normalized)) ||
                                               (s.Product.Name != null && s.Product.Name.ToLower().Contains(normalized)) )) ||
                        (s.Price != null && s.Price.Name != null && s.Price.Name.ToLower().Contains(normalized)));
                }

                var items = await query
                    .OrderByDescending(s => s.CreatedTime)
                    .Take(200)
                    .Select(s => new AdminSubscriptionItemViewModel
                    {
                        Id = s.Id,
                        UserName = s.User != null ? s.User.UserName : string.Empty,
                        UserEmail = s.User != null ? s.User.Email : string.Empty,
                        ProductCode = s.Product != null ? s.Product.Code : string.Empty,
                        ProductName = s.Product != null ? s.Product.Name : string.Empty,
                        PriceName = s.Price != null ? s.Price.Name : string.Empty,
                        Status = s.Status,
                        AutoRenew = s.AutoRenew,
                        CreatedTime = s.CreatedTime
                    })
                    .ToListAsync();

                var model = new AdminSubscriptionsPageViewModel
                {
                    Keyword = keyword,
                    Status = status,
                    Items = items
                };

                Log.Information("Admin subscriptions page accessed by {User}, loaded {Count} rows", User.Identity?.Name, items.Count);
                return View(model);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading admin subscriptions page");
                return View("Error");
            }
        }

        /// <summary>
        /// 管理后台交易列表。
        /// Admin transaction list page.
        /// </summary>
        /// <param name="gateway">网关筛选。 / Gateway filter.</param>
        /// <param name="status">状态筛选。 / Status filter.</param>
        /// <returns>交易列表视图。 / Transaction list view.</returns>
        [HttpGet]
        [Route("admin/transactions")]
        public async Task<IActionResult> Transactions([FromQuery] string? gateway, [FromQuery] short? status)
        {
            if (gateway != null && gateway.Length > 50)
            {
                throw new ArgumentException("gateway length cannot exceed 50", nameof(gateway));
            }

            try
            {
                var query = _db.Transactions
                    .AsNoTracking()
                    .Include(t => t.User)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(gateway))
                {
                    query = query.Where(t => t.Gateway.ToLower() == gateway.Trim().ToLower());
                }

                if (status.HasValue)
                {
                    query = query.Where(t => t.Status == status.Value);
                }

                var items = await query
                    .OrderByDescending(t => t.CreatedTime)
                    .Take(300)
                    .Select(t => new AdminTransactionItemViewModel
                    {
                        Id = t.Id,
                        UserName = t.User != null ? t.User.UserName : string.Empty,
                        UserEmail = t.User != null ? t.User.Email : string.Empty,
                        Amount = t.Amount,
                        Currency = t.Currency,
                        Gateway = t.Gateway,
                        Status = t.Status,
                        ExternalTransactionId = t.ExternalTransactionId,
                        CreatedTime = t.CreatedTime
                    })
                    .ToListAsync();

                var model = new AdminTransactionsPageViewModel
                {
                    Gateway = gateway,
                    Status = status,
                    Items = items
                };

                Log.Information("Admin transactions page accessed by {User}, loaded {Count} rows", User.Identity?.Name, items.Count);
                return View(model);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading admin transactions page");
                return View("Error");
            }
        }
    }
}
