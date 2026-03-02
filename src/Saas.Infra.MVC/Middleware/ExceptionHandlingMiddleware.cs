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

		public async Task InvokeAsync(HttpContext context)
		{
			try
			{
				// 执行后续中间件
				await _next(context);
			}
			catch (Exception ex)
			{
				// 记录异常日志（Serilog + 内置日志双保险）
				_logger.LogError(ex, "全局异常捕获：{ErrorMessage}", ex.Message);
				Log.Error(ex, "Serilog 全局异常日志：{ErrorMessage}", ex.Message);

				// 统一异常响应格式
				context.Response.ContentType = "application/json";
				context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

				// 使用注入的_env判断环境，替代app.Environment
				var errorResponse = new
				{
					StatusCode = context.Response.StatusCode,
					Message = _env.IsDevelopment() ? ex.Message : "服务器内部错误，请联系管理员",
					Detail = _env.IsDevelopment() ? ex.StackTrace : string.Empty,
					Timestamp = DateTime.UtcNow
				};

				// 确保JSON序列化配置（避免循环引用等问题）
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