using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Core;
using Saas.Infra.MVC.Security;
using Saas.Infra.Services.Payment;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Api
{
    /// <summary>
    /// 后台交易管理API控制器。
    /// Admin transaction management API controller.
    /// </summary>
    [ApiController]
    [Route("api/admin-transactions")]
    [AuthorizeRole(UserRole.Admin)]
    public class AdminTransactionsController : ControllerBase
    {
        private readonly IAdminTransactionExportService _adminTransactionExportService;

        /// <summary>
        /// 初始化<see cref="AdminTransactionsController"/>的新实例。
        /// Initializes a new instance of the <see cref="AdminTransactionsController"/> class.
        /// </summary>
        /// <param name="adminTransactionExportService">后台交易导出服务。 / Admin transaction export service.</param>
        /// <exception cref="ArgumentNullException">当 adminTransactionExportService 为空时抛出。 / Thrown when adminTransactionExportService is null.</exception>
        public AdminTransactionsController(IAdminTransactionExportService adminTransactionExportService)
        {
            _adminTransactionExportService = adminTransactionExportService ?? throw new ArgumentNullException(nameof(adminTransactionExportService));
        }

        /// <summary>
        /// 导出后台交易CSV。
        /// Exports admin transactions as CSV.
        /// </summary>
        /// <param name="gateway">网关筛选。 / Gateway filter.</param>
        /// <param name="status">状态筛选。 / Status filter.</param>
        /// <param name="fromDate">开始日期。 / From date.</param>
        /// <param name="toDate">结束日期。 / To date.</param>
        /// <returns>CSV文件。 / CSV file.</returns>
        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] string? gateway,
            [FromQuery] short? status,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            try
            {
                var result = await _adminTransactionExportService.ExportCsvAsync(gateway, status, fromDate, toDate);
                Log.Information("Admin transactions CSV exported by {Operator}", User.Identity?.Name);
                return File(result.Content, result.ContentType, result.FileName);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 导出后台交易Excel。
        /// Exports admin transactions as Excel.
        /// </summary>
        /// <param name="gateway">网关筛选。 / Gateway filter.</param>
        /// <param name="status">状态筛选。 / Status filter.</param>
        /// <param name="fromDate">开始日期。 / From date.</param>
        /// <param name="toDate">结束日期。 / To date.</param>
        /// <returns>Excel文件。 / Excel file.</returns>
        [HttpGet("export-excel")]
        public async Task<IActionResult> ExportExcel(
            [FromQuery] string? gateway,
            [FromQuery] short? status,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            try
            {
                var result = await _adminTransactionExportService.ExportExcelAsync(gateway, status, fromDate, toDate);
                Log.Information("Admin transactions Excel exported by {Operator}", User.Identity?.Name);
                return File(result.Content, result.ContentType, result.FileName);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
