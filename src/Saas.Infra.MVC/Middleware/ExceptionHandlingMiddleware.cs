using Serilog;
using System.Net;
using System.Text.Json;

namespace Saas.Infra.MVC.Middleware
{
	/// <summary>
	/// Global exception handling middleware for centralized error processing
	/// 全局异常处理中间件，用于集中处理应用异常
	/// </summary>
	public class ExceptionHandlingMiddleware
	{
		/// <summary>
		/// Next middleware delegate
		/// 下一个中间件委托
		/// </summary>
		private readonly RequestDelegate _next;

		/// <summary>
		/// Logger instance
		/// 日志记录器实例
		/// </summary>
		private readonly ILogger<ExceptionHandlingMiddleware> _logger;

		/// <summary>
		/// Web host environment information
		/// Web主机环境信息
		/// </summary>
		private readonly IWebHostEnvironment _env;

		/// <summary>
		/// Constructor for dependency injection
		/// 构造函数用于依赖注入
		/// </summary>
		/// <param name="next">Next middleware delegate / 下一个中间件委托</param>
		/// <param name="logger">Logger instance / 日志记录器实例</param>
		/// <param name="env">Web host environment / Web主机环境</param>
		/// <exception cref="ArgumentNullException">Thrown when any parameter is null / 当任何参数为null时抛出</exception>
		public ExceptionHandlingMiddleware(
			RequestDelegate next,
			ILogger<ExceptionHandlingMiddleware> logger,
			IWebHostEnvironment env)
		{
			_next = next ?? throw new ArgumentNullException(nameof(next), "RequestDelegate cannot be null");
			_logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger cannot be null");
			_env = env ?? throw new ArgumentNullException(nameof(env), "IWebHostEnvironment cannot be null");
		}

		/// <summary>
		/// Invoke middleware to handle exceptions
		/// 调用中间件处理异常
		/// </summary>
		/// <param name="context">HTTP context / HTTP上下文</param>
		/// <returns>Async task / 异步任务</returns>
		/// <exception cref="ArgumentNullException">Thrown when context is null / 当context为null时抛出</exception>
		public async Task InvokeAsync(HttpContext context)
		{
			if (context == null)
			{
				throw new ArgumentNullException(nameof(context), "HttpContext cannot be null");
			}

			try
			{
				// Execute next middleware
				await _next(context);
			}
			catch (Exception ex)
			{
				// Log exception with both Serilog and built-in logger
				_logger.LogError(ex, "Global exception caught: {ErrorMessage}", ex.Message);
				Log.Error(ex, "Serilog global exception log: {ErrorMessage}", ex.Message);

				// Set response content type and status code
				context.Response.ContentType = "application/json";
				context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

				// Build error response based on environment
				var errorResponse = new
				{
					StatusCode = context.Response.StatusCode,
					Message = _env.IsDevelopment() 
						? ex.Message 
						: "Internal server error. Please contact administrator.",
					Detail = _env.IsDevelopment() ? ex.StackTrace : string.Empty,
					Timestamp = DateTime.UtcNow
				};

				// Serialize and write response
				var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
				await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, options));
			}
		}
	}

	/// <summary>
	/// Extension methods for exception handling middleware registration
	/// 异常处理中间件注册的扩展方法
	/// </summary>
	public static class ExceptionHandlingMiddlewareExtensions
	{
		/// <summary>
		/// Register exception handling middleware
		/// 注册异常处理中间件
		/// </summary>
		/// <param name="builder">Application builder / 应用构建器</param>
		/// <returns>Application builder for chaining / 用于链式调用的应用构建器</returns>
		/// <exception cref="ArgumentNullException">Thrown when builder is null / 当builder为null时抛出</exception>
		public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder)
		{
			if (builder == null)
			{
				throw new ArgumentNullException(nameof(builder), "IApplicationBuilder cannot be null");
			}

			return builder.UseMiddleware<ExceptionHandlingMiddleware>();
		}
	}
}
