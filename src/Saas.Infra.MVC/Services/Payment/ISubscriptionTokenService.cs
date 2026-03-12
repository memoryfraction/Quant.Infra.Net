using System;

namespace Saas.Infra.MVC.Services.Payment
{
    /// <summary>
    /// 订阅令牌服务接口，用于生成订阅访问令牌。
    /// Subscription token service interface for generating subscription access tokens.
    /// </summary>
    public interface ISubscriptionTokenService
    {
        /// <summary>
        /// 生成订阅访问令牌（JWT）。
        /// Generates a subscription access token (JWT).
        /// </summary>
        /// <param name="request">令牌生成请求参数。 / Token generation request parameters.</param>
        /// <returns>访问令牌和过期秒数。 / Access token and expiry in seconds.</returns>
        SubscriptionTokenResult GenerateToken(SubscriptionTokenRequest request);
    }

    /// <summary>
    /// 订阅令牌生成请求参数。
    /// Subscription token generation request parameters.
    /// </summary>
    public class SubscriptionTokenRequest
    {
        /// <summary>
        /// 用户ID。 / User ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 用户邮箱。 / User email.
        /// </summary>
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// 产品ID。 / Product ID.
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// 产品名称。 / Product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// 订阅ID。 / Subscription ID.
        /// </summary>
        public Guid SubscriptionId { get; set; }

        /// <summary>
        /// 订阅状态。 / Subscription status.
        /// </summary>
        public short SubscriptionStatus { get; set; }

        /// <summary>
        /// 订阅开始时间（UTC）。 / Subscription start time (UTC).
        /// </summary>
        public DateTimeOffset SubscriptionStartUtc { get; set; }

        /// <summary>
        /// 订阅结束时间（UTC）。 / Subscription end time (UTC).
        /// </summary>
        public DateTimeOffset? SubscriptionEndUtc { get; set; }

        /// <summary>
        /// 订单ID。 / Order ID.
        /// </summary>
        public Guid OrderId { get; set; }
    }

    /// <summary>
    /// 订阅令牌生成结果。
    /// Subscription token generation result.
    /// </summary>
    public class SubscriptionTokenResult
    {
        /// <summary>
        /// 访问令牌。 / Access token.
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// 令牌过期秒数。 / Token expiry in seconds.
        /// </summary>
        public int ExpiresIn { get; set; }
    }
}
