using Serilog;
using System.Net;
using System.Text.Json;
using Saas.Infra.MVC.Services.Errors;

namespace Saas.Infra.MVC.Middleware
{
	// 全局异常处理中间件（单独抽离，复用性更高）
	public class ExceptionHandlingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<ExceptionHandlingMiddleware> _logger;
		private readonly IWebHostEnvironment _env;
		private readonly GlobalExceptionPageService _globalExceptionPageService;

		// 构造函数注入IWebHostEnvironment
		public ExceptionHandlingMiddleware(
			RequestDelegate next,
			ILogger<ExceptionHandlingMiddleware> logger,
			IWebHostEnvironment env,
			GlobalExceptionPageService globalExceptionPageService)
		{
			_next = next ?? throw new ArgumentNullException(nameof(next));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_env = env ?? throw new ArgumentNullException(nameof(env));
			_globalExceptionPageService = globalExceptionPageService ?? throw new ArgumentNullException(nameof(globalExceptionPageService));
		}

        /// <summary>
        /// 中间件入口，捕获下游未处理异常并路由到全局异常页或统一错误响应。
        /// Middleware entry point that catches downstream unhandled exceptions and routes them to the global error page or a uniform error response.
        /// </summary>
        /// <param name="context">当前 HTTP 上下文。/ The current HTTP context.</param>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Global exception caught: {ErrorMessage}", ex.Message);
                Log.Error(ex, "Serilog global exception log: {ErrorMessage}", ex.Message);

                var statusCode = (int)HttpStatusCode.InternalServerError;
                var errorId = _globalExceptionPageService.StoreException(ex, context, statusCode);

				if (ShouldRedirectToErrorPage(context))
				{
					context.Response.Clear();
					context.Response.Redirect($"/error?errorId={Uri.EscapeDataString(errorId)}");
					return;
				}

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = statusCode;

                var errorResponse = new
                {
                    ErrorId = errorId,
                    StatusCode = statusCode,
                    Message = ex.Message,
                    Detail = ex.ToString(),
                    Environment = _env.EnvironmentName,
                    Timestamp = DateTime.UtcNow
                };

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
            }
        }

		private static bool ShouldRedirectToErrorPage(HttpContext context)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));

			if (context.Request.Path.StartsWithSegments("/api")
				|| context.Request.Path.StartsWithSegments("/_blazor")
				|| context.Request.Path.StartsWithSegments("/_framework"))
			{
				return false;
			}

			var acceptsHtml = context.Request.Headers.Accept.Any(value => value.Contains("text/html", StringComparison.OrdinalIgnoreCase));
			return acceptsHtml;
		}
	}

	// 扩展方法：简化中间件注册
	public static class ExceptionHandlingMiddlewareExtensions
	{
        /// <summary>
        /// 注册全局异常处理中间件。
        /// Registers the global exception handling middleware.
        /// </summary>
        /// <param name="builder">应用程序构建器。 / Application builder.</param>
        /// <returns>应用程序构建器。 / Application builder.</returns>
        /// <exception cref="ArgumentNullException">当 builder 为空时抛出。 / Thrown when builder is null.</exception>
		public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder)
		{
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

			return builder.UseMiddleware<ExceptionHandlingMiddleware>();
		}
	}
}