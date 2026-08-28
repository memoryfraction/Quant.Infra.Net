using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Notification.Service;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Microsoft.Extensions.Logging;

namespace Quant.Infra.Net.Orchestration.Notifications;

/// <summary>
/// 路由通知网关：按严重级别把通知分发到已配置通道（钉钉/企微/邮件）。
/// Routing notification hub: severity-routed delivery to configured channels (DingTalk / WeChat Work / Email).
/// 级别 → 通道：Info=钉钉；Warning=钉钉+企微；Critical=钉钉+企微+邮件。
/// Severity → channels: Info=DingTalk; Warning=DingTalk+WeChat; Critical=DingTalk+WeChat+Email.
/// 通道未注册/未配置 → 静默跳过（记日志）；任何通道失败 → 捕获并记录，绝不抛出（通知失败不能杀死管道）。
/// Unregistered/unconfigured channels are silently skipped (logged); any channel failure is captured and never rethrown (a notification failure must not kill the pipeline).
/// </summary>
public sealed class RoutingNotificationHub : INotificationHub
{
    private readonly IDingtalkService? _dingtalk;
    private readonly IWeChatService? _weChat;
    private readonly IEmailService? _email;
    private readonly OrchestrationOptions _options;
    private readonly ILogger? _logger;

    /// <summary>
    /// 最近一次 PublishAsync 中跳过/失败的通道名（测试可观测；生产可忽略）。
    /// Channels skipped or failed during the last PublishAsync (test observability; safe to ignore in production).
    /// </summary>
    public IReadOnlyList<string> LastSkippedOrFailed { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// 创建路由通知网关。
    /// Creates the routing notification hub.
    /// </summary>
    /// <param name="dingtalk">钉钉服务（可为 null）/ DingTalk service (may be null).</param>
    /// <param name="weChat">企微服务（可为 null）/ WeChat Work service (may be null).</param>
    /// <param name="email">邮件服务（可为 null）/ Email service (may be null).</param>
    /// <param name="options">编排配置（不得为 null）/ Orchestration options (must not be null).</param>
    /// <param name="logger">日志器（可为 null）/ Logger (may be null).</param>
    /// <exception cref="ArgumentNullException">options 为 null 时抛出 / Thrown when options is null.</exception>
    public RoutingNotificationHub(IDingtalkService? dingtalk, IWeChatService? weChat, IEmailService? email, OrchestrationOptions options, ILogger? logger = null)
    {
        _dingtalk = dingtalk;
        _weChat = weChat;
        _email = email;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync(NotificationSeverity severity, string title, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title must not be blank.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be blank.", nameof(message));
        }

        ct.ThrowIfCancellationRequested();

        var text = BuildBody(severity, title, message);
        var skippedOrFailed = new List<string>(4);
        var nav = _options.Notifications;
        if (!nav.Enabled)
        {
            Finish(skippedOrFailed);
            return;
        }

        switch (severity)
        {
            case NotificationSeverity.Info:
                await SendDingtalkAsync(text, nav, skippedOrFailed, ct).ConfigureAwait(false);
                break;
            case NotificationSeverity.Warning:
                await SendDingtalkAsync(text, nav, skippedOrFailed, ct).ConfigureAwait(false);
                await SendWeChatAsync(text, nav, skippedOrFailed, ct).ConfigureAwait(false);
                break;
            case NotificationSeverity.Critical:
            default:
                await SendDingtalkAsync(text, nav, skippedOrFailed, ct).ConfigureAwait(false);
                await SendWeChatAsync(text, nav, skippedOrFailed, ct).ConfigureAwait(false);
                await SendEmailAsync(text, title, nav, skippedOrFailed, ct).ConfigureAwait(false);
                break;
        }

        Finish(skippedOrFailed);
    }

    private void Finish(List<string> skippedOrFailed)
    {
        LastSkippedOrFailed = skippedOrFailed;
        if (skippedOrFailed.Count > 0)
        {
            _logger?.LogInformation("notification skipped/failed channels: {Channels}", string.Join(", ", skippedOrFailed));
        }
    }

    private async Task SendDingtalkAsync(string text, NotificationOptions nav, List<string> sink, CancellationToken ct)
    {
        if (_dingtalk == null)
        {
            sink.Add("dingtalk (not registered)");
            return;
        }

        if (string.IsNullOrWhiteSpace(nav.DingtalkAccessToken))
        {
            sink.Add("dingtalk (token missing)");
            return;
        }

        try
        {
            await _dingtalk.SendNotificationAsync(text, nav.DingtalkAccessToken, nav.DingtalkSecret ?? string.Empty).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sink.Add("dingtalk: " + ex.Message);
        }
    }

    private async Task SendWeChatAsync(string text, NotificationOptions nav, List<string> sink, CancellationToken ct)
    {
        if (_weChat == null)
        {
            sink.Add("weChatWork (not registered)");
            return;
        }

        if (string.IsNullOrWhiteSpace(nav.WeChatWebHook))
        {
            sink.Add("weChatWork (webhook missing)");
            return;
        }

        try
        {
            await _weChat.SendTextNotificationAsync(text, nav.WeChatWebHook).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sink.Add("weChatWork: " + ex.Message);
        }
    }

    private async Task SendEmailAsync(string body, string title, NotificationOptions nav, List<string> sink, CancellationToken ct)
    {
        if (_email == null)
        {
            sink.Add("email (not registered)");
            return;
        }

        if (nav.EmailRecipients.Length == 0 || string.IsNullOrWhiteSpace(nav.EmailSender))
        {
            sink.Add("email (recipients/sender missing)");
            return;
        }

        try
        {
            var message = new EmailMessage
            {
                To = nav.EmailRecipients.Where(r => !string.IsNullOrWhiteSpace(r)).ToList(),
                Subject = title,
                Body = body,
                IsHtml = false
            };

            var setting = new PersonalEmailSetting
            {
                SmtpServer = nav.EmailSmtpServer ?? string.Empty,
                Port = nav.EmailPort,
                SenderEmail = nav.EmailSender,
                SenderName = "Quant.Infra.Net Orchestration",
                Username = nav.EmailUsername ?? string.Empty,
                Password = nav.EmailPassword ?? string.Empty
            };

            await _email.SendBulkEmailAsync(message, setting).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sink.Add("email: " + ex.Message);
        }
    }

    private static string BuildBody(NotificationSeverity severity, string title, string message)
        => $"[{severity}] {title}: {message}";
}
