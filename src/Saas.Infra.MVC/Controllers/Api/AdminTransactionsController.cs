using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Saas.Infra.MVC.Security;
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
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// 初始化<see cref="AdminTransactionsController"/>的新实例。
        /// Initializes a new instance of the <see cref="AdminTransactionsController"/> class.
        /// </summary>
        /// <param name="db">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当 db 为空时抛出。 / Thrown when db is null.</exception>
        public AdminTransactionsController(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
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
            if (!IsValidFilter(gateway, fromDate, toDate, out var errorResult))
            {
                return errorResult!;
            }

            var items = await QueryTransactionsAsync(gateway, status, fromDate, toDate).ConfigureAwait(false);

            var builder = new StringBuilder();
            builder.AppendLine("TransactionId,UserName,UserEmail,Amount,Currency,Gateway,Status,ExternalTransactionId,SubscriptionId,CreatedTime,Remarks");

            foreach (var item in items)
            {
                builder.AppendLine(string.Join(",",
                    EscapeCsv(item.Id.ToString()),
                    EscapeCsv(item.UserName),
                    EscapeCsv(item.UserEmail),
                    EscapeCsv((item.Amount / 100.0m).ToString("F2")),
                    EscapeCsv(item.Currency),
                    EscapeCsv(item.Gateway),
                    EscapeCsv(GetStatusText(item.Status)),
                    EscapeCsv(item.ExternalTransactionId ?? string.Empty),
                    EscapeCsv(item.SubscriptionId?.ToString() ?? string.Empty),
                    EscapeCsv(item.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss zzz")),
                    EscapeCsv(item.Remarks ?? string.Empty)));
            }

            Log.Information("Admin transactions CSV exported by {Operator}, count {Count}", User.Identity?.Name, items.Count);

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            var fileName = $"transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv", fileName);
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
            if (!IsValidFilter(gateway, fromDate, toDate, out var errorResult))
            {
                return errorResult!;
            }

            var items = await QueryTransactionsAsync(gateway, status, fromDate, toDate).ConfigureAwait(false);
            var xml = BuildExcelXml(items);

            Log.Information("Admin transactions Excel exported by {Operator}, count {Count}", User.Identity?.Name, items.Count);

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(xml)).ToArray();
            var fileName = $"transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.xml";
            return File(bytes, "application/vnd.ms-excel", fileName);
        }

        private bool IsValidFilter(string? gateway, DateTime? fromDate, DateTime? toDate, out IActionResult? errorResult)
        {
            errorResult = null;

            if (!string.IsNullOrWhiteSpace(gateway) && gateway.Length > 50)
            {
                errorResult = BadRequest(new { message = "Gateway length cannot exceed 50 characters" });
                return false;
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            {
                errorResult = BadRequest(new { message = "From date cannot be later than to date" });
                return false;
            }

            return true;
        }

        private async Task<List<ExportTransactionItem>> QueryTransactionsAsync(string? gateway, short? status, DateTime? fromDate, DateTime? toDate)
        {
            var query = _db.Transactions
                .AsNoTracking()
                .Include(t => t.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(gateway))
            {
                var normalizedGateway = gateway.Trim().ToLowerInvariant();
                query = query.Where(t => t.Gateway.ToLower() == normalizedGateway);
            }

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (fromDate.HasValue)
            {
                var fromDateOffset = new DateTimeOffset(fromDate.Value.Date, TimeSpan.Zero);
                query = query.Where(t => t.CreatedTime >= fromDateOffset);
            }

            if (toDate.HasValue)
            {
                var toExclusiveDateOffset = new DateTimeOffset(toDate.Value.Date.AddDays(1), TimeSpan.Zero);
                query = query.Where(t => t.CreatedTime < toExclusiveDateOffset);
            }

            return await query
                .OrderByDescending(t => t.CreatedTime)
                .Take(5000)
                .Select(t => new ExportTransactionItem
                {
                    Id = t.Id,
                    UserName = t.User != null ? (t.User.UserName ?? string.Empty) : string.Empty,
                    UserEmail = t.User != null ? (t.User.Email ?? string.Empty) : string.Empty,
                    Amount = t.Amount,
                    Currency = t.Currency,
                    Gateway = t.Gateway,
                    Status = t.Status,
                    ExternalTransactionId = t.ExternalTransactionId,
                    SubscriptionId = t.SubscriptionId,
                    CreatedTime = t.CreatedTime,
                    Remarks = t.Remarks
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }

        private static string BuildExcelXml(IEnumerable<ExportTransactionItem> items)
        {
            var spreadsheetNamespace = XNamespace.Get("urn:schemas-microsoft-com:office:spreadsheet");
            var workbook = new XElement(spreadsheetNamespace + "Workbook",
                new XAttribute(XNamespace.Xmlns + "ss", spreadsheetNamespace.NamespaceName),
                new XElement(spreadsheetNamespace + "Worksheet",
                    new XAttribute(spreadsheetNamespace + "Name", "Transactions"),
                    new XElement(spreadsheetNamespace + "Table",
                        CreateRow(spreadsheetNamespace,
                            "Transaction ID",
                            "User Name",
                            "User Email",
                            "Amount",
                            "Currency",
                            "Gateway",
                            "Status",
                            "External Transaction ID",
                            "Subscription ID",
                            "Created Time",
                            "Remarks"),
                        items.Select(item => CreateRow(spreadsheetNamespace,
                            item.Id.ToString(),
                            item.UserName,
                            item.UserEmail,
                            (item.Amount / 100.0m).ToString("F2"),
                            item.Currency,
                            item.Gateway,
                            GetStatusText(item.Status),
                            item.ExternalTransactionId ?? string.Empty,
                            item.SubscriptionId?.ToString() ?? string.Empty,
                            item.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            item.Remarks ?? string.Empty)))));

            return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), workbook).ToString();
        }

        private static XElement CreateRow(XNamespace spreadsheetNamespace, params string[] values)
        {
            return new XElement(spreadsheetNamespace + "Row",
                values.Select(value =>
                    new XElement(spreadsheetNamespace + "Cell",
                        new XElement(spreadsheetNamespace + "Data",
                            new XAttribute(spreadsheetNamespace + "Type", "String"),
                            value ?? string.Empty))));
        }

        private static string EscapeCsv(string value)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static string GetStatusText(short status)
        {
            return status switch
            {
                0 => "Pending",
                1 => "Success",
                2 => "Failed",
                3 => "Refunded",
                _ => "Unknown"
            };
        }

        private sealed class ExportTransactionItem
        {
            public Guid Id { get; set; }

            public string UserName { get; set; } = string.Empty;

            public string UserEmail { get; set; } = string.Empty;

            public long Amount { get; set; }

            public string Currency { get; set; } = string.Empty;

            public string Gateway { get; set; } = string.Empty;

            public short Status { get; set; }

            public string? ExternalTransactionId { get; set; }

            public Guid? SubscriptionId { get; set; }

            public DateTimeOffset CreatedTime { get; set; }

            public string? Remarks { get; set; }
        }
    }
}
