using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Saas.Infra.MVC.Middleware
{
    /// <summary>
    /// Bearer方案的最小认证处理器占位符。不执行实际认证，认证由CustomJwtMiddleware处理。仅参与Challenge/Forbid以产生适当的HTTP响应。
    /// A minimal authentication handler that acts as a placeholder for the Bearer scheme. Does not perform authentication; authentication is handled by CustomJwtMiddleware. Only participates in Challenge/Forbid to produce appropriate HTTP responses.
    /// </summary>
    public class DummyJwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        /// <summary>
        /// 初始化<see cref="DummyJwtAuthenticationHandler"/>的新实例。
        /// Initializes a new instance of the <see cref="DummyJwtAuthenticationHandler"/> class.
        /// </summary>
        /// <param name="options">认证方案选项监视器。 / Authentication scheme options monitor.</param>
        /// <param name="logger">日志记录器工厂。 / Logger factory.</param>
        /// <param name="encoder">URL编码器。 / URL encoder.</param>
        /// <param name="clock">系统时钟。 / System clock.</param>
        /// <exception cref="ArgumentNullException">当必需的参数为null时抛出。 / Thrown when required parameters are null.</exception>
        public DummyJwtAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (encoder == null) throw new ArgumentNullException(nameof(encoder));
            if (clock == null) throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>
        /// 处理认证请求。从CustomJwtMiddleware设置的HttpContext.User读取身份。
        /// Handles authentication request. Reads identity from HttpContext.User set by CustomJwtMiddleware.
        /// </summary>
        /// <returns>认证结果任务。 / Task containing authentication result.</returns>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // CustomJwtMiddleware已经设置了HttpContext.User
            // 如果User已认证，返回成功结果
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var ticket = new AuthenticationTicket(Context.User, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            // 否则返回NoResult，让其他认证方案处理
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        /// <summary>
        /// 处理认证挑战，返回401 Unauthorized响应。
        /// Handles authentication challenge and returns 401 Unauthorized response.
        /// </summary>
        /// <param name="properties">认证属性。 / Authentication properties.</param>
        /// <returns>完成任务。 / Completed task.</returns>
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            if (!Response.Headers.ContainsKey("WWW-Authenticate"))
            {
                Response.Headers.Append("WWW-Authenticate", "Bearer");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 处理禁止访问请求，返回403 Forbidden响应。
        /// Handles forbidden request and returns 403 Forbidden response.
        /// </summary>
        /// <param name="properties">认证属性。 / Authentication properties.</param>
        /// <returns>完成任务。 / Completed task.</returns>
        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}
