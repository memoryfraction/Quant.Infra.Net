namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 管道结构化事件：运行 ID、时间（UTC）、阶段、消息与可选数据。
/// Structured pipeline event: run id, UTC timestamp, stage, message, and optional data.
/// </summary>
/// <remarks>
/// 只能通过 <see cref="Create"/> 工厂方法创建，保证时间戳统一取 UtcNow。
/// Instances can only be created via the <see cref="Create"/> factory so the timestamp is always UtcNow.
/// </remarks>
public sealed class PipelineEvent
{
    /// <summary>
    /// 构造函数为私有：请使用 <see cref="Create"/>。
    /// Constructor is private: use <see cref="Create"/> instead.
    /// </summary>
    private PipelineEvent()
    {
    }

    /// <summary>
    /// 创建一条管道事件（时间戳取当前 UTC）。
    /// Creates a pipeline event with the current UTC timestamp.
    /// </summary>
    /// <param name="runId">所属运行 ID / Owning run id.</param>
    /// <param name="stage">阶段名（不得为空白）/ Stage name (must not be blank).</param>
    /// <param name="message">事件消息（不得为 null）/ Event message (must not be null).</param>
    /// <param name="data">可选结构化数据 / Optional structured data.</param>
    /// <param name="severity">事件严重级别，默认 Info / Event severity, defaults to Info.</param>
    /// <returns>新事件实例 / A new event instance.</returns>
    /// <exception cref="ArgumentException">stage 为空白时抛出 / Thrown when stage is blank.</exception>
    /// <exception cref="ArgumentNullException">message 为 null 时抛出 / Thrown when message is null.</exception>
    public static PipelineEvent Create(long runId, string stage, string message, object? data = null, NotificationSeverity severity = NotificationSeverity.Info)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            throw new ArgumentException("Stage name must not be blank.", nameof(stage));
        }
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        return new PipelineEvent
        {
            RunId = runId,
            TimestampUtc = DateTime.UtcNow,
            Stage = stage,
            Message = message,
            Data = data,
            Severity = severity
        };
    }

    /// <summary>
    /// 运行 ID。
    /// Owning run id.
    /// </summary>
    public long RunId { get; private init; }

    /// <summary>
    /// 事件时间（UTC）。
    /// Event timestamp (UTC).
    /// </summary>
    public DateTime TimestampUtc { get; private init; }

    /// <summary>
    /// 阶段名。
    /// Stage name.
    /// </summary>
    public string Stage { get; private init; } = string.Empty;

    /// <summary>
    /// 事件消息。
    /// Event message.
    /// </summary>
    public string Message { get; private init; } = string.Empty;

    /// <summary>
    /// 可选结构化数据。
    /// Optional structured data.
    /// </summary>
    public object? Data { get; private init; }

    /// <summary>
    /// 事件严重级别（默认 Info）。
    /// Event severity (defaults to Info).
    /// </summary>
    public NotificationSeverity Severity { get; private init; } = NotificationSeverity.Info;
}
