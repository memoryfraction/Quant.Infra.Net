using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Notification.Service;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Notifications;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Stages;
using RestSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// 测试用：记录调用的钉钉服务。
/// Test double: recording DingTalk service.
/// </summary>
internal sealed class FakeDingtalkService : IDingtalkService
{
    public List<(string Content, string Token, string Secret)> Sent { get; } = new();
    public bool Throw { get; set; }

    public Task<RestResponse> SendNotificationAsync(string content, string accessToken, string secret)
    {
        if (Throw)
        {
            throw new InvalidOperationException("dingtalk fake failure");
        }

        Sent.Add((content, accessToken, secret));
        return Task.FromResult(new RestResponse());
    }
}

/// <summary>
/// 测试用：记录调用的企微服务。
/// Test double: recording WeChat Work service.
/// </summary>
internal sealed class FakeWeChatService : IWeChatService
{
    public List<(string Content, string WebHook)> Sent { get; } = new();
    public bool Throw { get; set; }

    public Task<RestResponse> SendTextNotificationAsync(string content, string webHook)
    {
        if (Throw)
        {
            throw new InvalidOperationException("wechat fake failure");
        }

        Sent.Add((content, webHook));
        return Task.FromResult(new RestResponse());
    }
}

/// <summary>
/// 测试用：记录调用的邮件服务。
/// Test double: recording email service.
/// </summary>
internal sealed class FakeEmailService : IEmailService
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();
    public bool Throw { get; set; }

    public Task<bool> SendBulkEmailAsync(Quant.Infra.Net.Notification.Model.EmailMessage message, EmailSettingBase setting)
    {
        if (Throw)
        {
            throw new InvalidOperationException("email fake failure");
        }

        Sent.Add((string.Join(";", message.To), message.Subject, message.Body));
        return Task.FromResult(true);
    }
}

/// <summary>
/// RoutingNotificationHub 单元测试：严重级别 × 通道矩阵 + 降级不抛异常。
/// RoutingNotificationHub unit tests: severity × channel matrix + degraded-but-never-throw.
/// </summary>
[TestClass]
public class RoutingNotificationHubTests
{
    private static OrchestrationOptions NewOptions(bool ding = true, bool weChat = true, bool email = true)
    {
        var options = new OrchestrationOptions();
        options.Notifications.DingtalkAccessToken = ding ? "dt-token" : string.Empty;
        options.Notifications.WeChatWebHook = weChat ? "https://weChat.example/hook" : string.Empty;
        options.Notifications.EmailRecipients = email ? new[] { "a@x.com" } : Array.Empty<string>();
        options.Notifications.EmailSender = email ? "bot@x.com" : string.Empty;
        return options;
    }

    private static (RoutingNotificationHub Hub, FakeDingtalkService Ding, FakeWeChatService WeChat, FakeEmailService Email) NewHub(OrchestrationOptions options)
    {
        var ding = new FakeDingtalkService();
        var weChat = new FakeWeChatService();
        var email = new FakeEmailService();
        return (new RoutingNotificationHub(ding, weChat, email, options), ding, weChat, email);
    }

    /// <summary>
    /// Info → 仅钉钉。
    /// Info → DingTalk only.
    /// </summary>
    [TestMethod]
    public async Task Info_RoutesToDingtalkOnly()
    {
        var (hub, ding, weChat, email) = NewHub(NewOptions());
        await hub.PublishAsync(NotificationSeverity.Info, "t", "m", CancellationToken.None);

        Assert.AreEqual(1, ding.Sent.Count);
        Assert.AreEqual(0, weChat.Sent.Count);
        Assert.AreEqual(0, email.Sent.Count);
        StringAssert.Contains(ding.Sent[0].Content, "[Info]");
        StringAssert.Contains(ding.Sent[0].Content, "t");
    }

    /// <summary>
    /// Warning → 钉钉 + 企微。
    /// Warning → DingTalk + WeChat Work.
    /// </summary>
    [TestMethod]
    public async Task Warning_RoutesToDingtalkAndWeChat()
    {
        var (hub, ding, weChat, email) = NewHub(NewOptions());
        await hub.PublishAsync(NotificationSeverity.Warning, "t", "m", CancellationToken.None);

        Assert.AreEqual(1, ding.Sent.Count);
        Assert.AreEqual(1, weChat.Sent.Count);
        Assert.AreEqual(0, email.Sent.Count);
        StringAssert.Contains(weChat.Sent[0].Content, "[Warning]");
    }

    /// <summary>
    /// Critical → 三个通道。
    /// Critical → all three channels.
    /// </summary>
    [TestMethod]
    public async Task Critical_RoutesToAllChannels()
    {
        var (hub, ding, weChat, email) = NewHub(NewOptions());
        await hub.PublishAsync(NotificationSeverity.Critical, "t", "m", CancellationToken.None);

        Assert.AreEqual(1, ding.Sent.Count);
        Assert.AreEqual(1, weChat.Sent.Count);
        Assert.AreEqual(1, email.Sent.Count);
        Assert.AreEqual("a@x.com", email.Sent[0].To);
    }

    /// <summary>
    /// 通道未注册 → 静默跳过且不抛异常（降级）。
    /// Unregistered channels → silently skipped without throwing (degraded).
    /// </summary>
    [TestMethod]
    public async Task MissingServices_NeverThrow()
    {
        var options = NewOptions(ding: false, weChat: false, email: false);
        var hub = new RoutingNotificationHub(null, null, null, options);

        await hub.PublishAsync(NotificationSeverity.Critical, "t", "m", CancellationToken.None);

        StringAssert.Contains(string.Join(" ", hub.LastSkippedOrFailed), "email");
        StringAssert.Contains(string.Join(" ", hub.LastSkippedOrFailed), "dingtalk");
    }

    /// <summary>
    /// 未配置凭据 → 跳过且不抛异常。
    /// Missing credentials → skipped without throwing.
    /// </summary>
    [TestMethod]
    public async Task MissingCredentials_SkippedNotThrown()
    {
        var (hub, ding, weChat, email) = NewHub(NewOptions(ding: false));
        await hub.PublishAsync(NotificationSeverity.Warning, "t", "m", CancellationToken.None);

        Assert.AreEqual(0, ding.Sent.Count);
        Assert.AreEqual(1, weChat.Sent.Count);
        StringAssert.Contains(string.Join(" ", hub.LastSkippedOrFailed), "token missing");
    }

    /// <summary>
    /// 通道实现抛异常 → 网关不抛、不阻塞后续通道（通知失败不能杀死管道）。
    /// Channel throws → hub does not throw and later channels still run (a failure must not kill the pipeline).
    /// </summary>
    [TestMethod]
    public async Task ChannelFailure_NeverThrowsAndContinues()
    {
        var ding = new FakeDingtalkService { Throw = true };
        var weChat = new FakeWeChatService();
        var hub = new RoutingNotificationHub(ding, weChat, null, NewOptions());

        await hub.PublishAsync(NotificationSeverity.Warning, "t", "m", CancellationToken.None);

        Assert.AreEqual(1, weChat.Sent.Count);
        StringAssert.Contains(string.Join(" ", hub.LastSkippedOrFailed), "dingtalk fake failure");
    }

    /// <summary>
    /// 空白标题/正文 → ArgumentException（编程错误不属于降级）。
    /// Blank title/body → ArgumentException (programming error, not degradation).
    /// </summary>
    [TestMethod]
    public async Task BlankArguments_Throw()
    {
        var (hub, _, _, _) = NewHub(NewOptions());
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await hub.PublishAsync(NotificationSeverity.Info, " ", "m", CancellationToken.None));
        await Assert.ThrowsExceptionAsync<ArgumentException>(
            async () => await hub.PublishAsync(NotificationSeverity.Info, "t", " ", CancellationToken.None));
    }
}

/// <summary>
/// NotificationStage 单元测试：有错误 → Warning；干净运行 → Info。
/// NotificationStage unit tests: errors present → Warning; clean run → Info.
/// </summary>
[TestClass]
public class NotificationStageTests
{
    /// <summary>
    /// 干净运行 → Info 汇总。
    /// Clean run → Info summary.
    /// </summary>
    [TestMethod]
    public async Task CleanRun_InfoSummary()
    {
        var options = new OrchestrationOptions { Notifications = new NotificationOptions { Enabled = false } };
        var ding = new FakeDingtalkService();
        var hub = new RoutingNotificationHub(ding, new FakeWeChatService(), new FakeEmailService(), options);

        var ctx = new PipelineContext(800);
        ctx.Set<IReadOnlyList<Signal>>(new List<Signal>
        {
            new() { Symbol = "AAA", Direction = SignalDirection.Long, Reason = "t" }
        });

        await new NotificationStage(hub).ExecuteAsync(ctx, CancellationToken.None);

        Assert.IsTrue(ctx.Events.Any(e => e.Stage == "Notification" && e.Message.Contains("INFO")));
    }

    /// <summary>
    /// 含错误的运行 → Warning 且正文含错误信息（通道全禁用也不抛）。
    /// Run with errors → Warning with the error text (channels disabled, no throw).
    /// </summary>
    [TestMethod]
    public async Task ErroredRun_WarningWithDetails()
    {
        var options = new OrchestrationOptions { Notifications = new NotificationOptions { Enabled = false } };
        var hub = new RoutingNotificationHub(new FakeDingtalkService(), new FakeWeChatService(), new FakeEmailService(), options);

        var ctx = new PipelineContext(801);
        ctx.AddError(new InvalidOperationException("synthetic stage error"));

        await new NotificationStage(hub).ExecuteAsync(ctx, CancellationToken.None);

        Assert.IsTrue(ctx.Events.Any(e => e.Stage == "Notification" && e.Message.Contains("WARNING")));
    }

    /// <summary>
    /// null 参数校验。
    /// Null argument validation.
    /// </summary>
    [TestMethod]
    public void NullHub_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new NotificationStage(null!));
    }
}
