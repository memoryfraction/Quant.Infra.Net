namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 通知严重级别：信息、告警、严重。
/// Notification severity levels: informational, warning, critical.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>
    /// 信息提示 / Informational.
    /// </summary>
    Info = 1,

    /// <summary>
    /// 告警 / Warning.
    /// </summary>
    Warning = 2,

    /// <summary>
    /// 严重（故障/紧急）/ Critical (failure/urgent).
    /// </summary>
    Critical = 3
}
