using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace Saas.Infra.MVC.Services.Errors
{
    /// <summary>
    /// 全局异常页面状态服务。
    /// Global exception page state service.
    /// </summary>
    public sealed class GlobalExceptionPageService
    {
        private readonly ConcurrentDictionary<string, GlobalExceptionEntry> _entries = new();

        /// <summary>
        /// 存储异常详情并返回错误标识。
        /// Stores exception details and returns an error identifier.
        /// </summary>
        /// <param name="exception">异常对象。 / Exception object.</param>
        /// <param name="context">HTTP上下文。 / HTTP context.</param>
        /// <param name="statusCode">HTTP状态码。 / HTTP status code.</param>
        /// <returns>错误标识。 / Error identifier.</returns>
        /// <exception cref="ArgumentNullException">当参数为空时抛出。 / Thrown when arguments are null.</exception>
        public string StoreException(Exception exception, HttpContext context, int statusCode)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            TrimExpiredEntries();

            var errorId = Guid.NewGuid().ToString("N");
            var entry = new GlobalExceptionEntry
            {
                ErrorId = errorId,
                StatusCode = statusCode,
                Message = exception.Message,
                Detail = exception.ToString(),
                Path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/",
                Method = context.Request.Method,
                CreatedTime = DateTimeOffset.UtcNow
            };

            _entries[errorId] = entry;
            return errorId;
        }

        /// <summary>
        /// 根据错误标识获取异常详情。
        /// Gets exception details by error identifier.
        /// </summary>
        /// <param name="errorId">错误标识。 / Error identifier.</param>
        /// <returns>异常详情。 / Exception details.</returns>
        /// <exception cref="ArgumentNullException">当 errorId 为空时抛出。 / Thrown when errorId is null or empty.</exception>
        public GlobalExceptionEntry? GetException(string errorId)
        {
            if (string.IsNullOrWhiteSpace(errorId))
                throw new ArgumentNullException(nameof(errorId));

            return _entries.TryGetValue(errorId, out var entry)
                ? entry
                : null;
        }

        private void TrimExpiredEntries()
        {
            var expiredBefore = DateTimeOffset.UtcNow.AddMinutes(-10);
            foreach (var item in _entries)
            {
                if (item.Value.CreatedTime < expiredBefore)
                {
                    _entries.TryRemove(item.Key, out _);
                }
            }
        }
    }

    /// <summary>
    /// 全局异常页面条目。
    /// Global exception page entry.
    /// </summary>
    public sealed class GlobalExceptionEntry
    {
        /// <summary>
        /// 错误标识。
        /// Error identifier.
        /// </summary>
        public string ErrorId { get; set; } = string.Empty;

        /// <summary>
        /// HTTP状态码。
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 异常消息。
        /// Exception message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 异常详细信息。
        /// Exception detail information.
        /// </summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>
        /// 请求路径。
        /// Request path.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 请求方法。
        /// Request method.
        /// </summary>
        public string Method { get; set; } = string.Empty;

        /// <summary>
        /// 记录创建时间。
        /// Entry created time.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }
    }
}
