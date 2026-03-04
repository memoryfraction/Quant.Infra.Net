using Serilog;
using System.Net;
using System.Text.Json;

namespace Saas.Infra.MVC.Middleware
{
	// 全局异常处理中间件（单独抽离，复用性更高）
	public class ExceptionHandlingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<ExceptionHandlingMiddleware> _logger;
		private readonly IWebHostEnvironment _env; // 注入环境信息，替代直接访问app.Environment

		// 构造函数注入IWebHostEnvironment
		public ExceptionHandlingMiddleware(
			RequestDelegate next,
			ILogger<ExceptionHandlingMiddleware> logger,
			IWebHostEnvironment env)
		{
			_next = next;
			_logger = logger;
			_env = env; // 接收环境信息
		}

        /// <summary>
        /// 中间件入口，捕获下游异常并转换为统一的 JSON 错误响应。
        /// Middleware entry point that catches downstream exceptions and converts them to a uniform JSON error response.
        /// </summary>
        /// <param name="context">当前 HTTP 上下文。/ The current HTTP context.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            try
            {
                // Execute next middleware
                await _next(context);
            }
            catch (Exception ex)
            {
                // Log exception (Serilog + injected logger)
                _logger.LogError(ex, "Global exception caught: {ErrorMessage}", ex.Message);
                Log.Error(ex, "Serilog global exception log: {ErrorMessage}", ex.Message);

                // Standardize error response
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var errorResponse = new
                {
                    StatusCode = context.Response.StatusCode,
                    Message = _env.IsDevelopment() ? ex.Message : "Internal server error, please contact the administrator",
                    Detail = _env.IsDevelopment() ? ex.StackTrace : string.Empty,
                    Timestamp = DateTime.UtcNow
                };

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
            }
        }
	}

	// 扩展方法：简化中间件注册
	public static class ExceptionHandlingMiddlewareExtensions
	{
		public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder)
		{
			return builder.UseMiddleware<ExceptionHandlingMiddleware>();
		}
	}
}