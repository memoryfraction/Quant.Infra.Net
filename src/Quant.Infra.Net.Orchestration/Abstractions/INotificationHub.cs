using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Abstractions;

/// <summary>
/// 通知中枢抽象：按严重级别路由到钉钉/企微/Email（通道未配置时静默跳过）。
/// Notification hub abstraction: routes messages to DingTalk/WeChat/Mail by severity (silently skips unconfigured channels).
/// </summary>
public interface INotificationHub
{
    /// <summary>
    /// 发布一条通知；本方法本身不得抛出任何异常（故障降级为日志）。
    /// Publishes one notification; this method itself must never throw (failures degrade to log output).
    /// </summary>
    /// <param name="severity">严重级别 / Severity.</param>
    /// <param name="title">标题（不得为 null）/ Title (must not be null).</param>
    /// <param name="message">正文（不得为 null）/ Message body (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>表示发布完成的任务 / Task representing the completed publish.</returns>
    Task PublishAsync(NotificationSeverity severity, string title, string message, CancellationToken ct);
}
