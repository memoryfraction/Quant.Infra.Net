namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 管道上下文接口：阶段间共享的类型化数据槽 + 参数读取 + 错误/事件累积。
/// Pipeline context interface: typed data slots shared between stages plus parameter access and error/event accumulation.
/// </summary>
public interface IPipelineContext
{
    /// <summary>
    /// 本次运行的唯一 ID。
    /// Unique identifier of the current pipeline run.
    /// </summary>
    long RunId { get; }

    /// <summary>
    /// 读取策略参数；不存在返回 null。
    /// Reads a strategy parameter; returns null when the key is absent.
    /// </summary>
    /// <param name="key">参数名 / Parameter name.</param>
    /// <returns>参数值（不存在为 null）/ Parameter value (null when absent).</returns>
    string? GetParameter(string key);

    /// <summary>
    /// 存入类型化数据槽（同类型后写覆盖）。
    /// Stores a typed data slot (a later write of the same type overwrites).
    /// </summary>
    /// <typeparam name="T">数据槽类型 / Data slot type.</typeparam>
    /// <param name="value">要存入的值（不得为 null）/ Value to store (must not be null).</param>
    void Set<T>(T value) where T : class;

    /// <summary>
    /// 读取类型化数据槽；未存入返回 null。
    /// Reads a typed data slot; returns null when nothing was stored.
    /// </summary>
    /// <typeparam name="T">数据槽类型 / Data slot type.</typeparam>
    /// <returns>已存入的值（未存入为 null）/ Stored value (null when absent).</returns>
    T? Get<T>() where T : class;

    /// <summary>
    /// 累积一次运行中的错误。
    /// Accumulates an error encountered during the run.
    /// </summary>
    /// <param name="error">异常实例（不得为 null）/ Exception instance (must not be null).</param>
    void AddError(Exception error);

    /// <summary>
    /// 本次运行累积的全部错误（只读）。
    /// All errors accumulated during the run (read-only).
    /// </summary>
    IReadOnlyList<Exception> Errors { get; }

    /// <summary>
    /// 追加一条管道事件（结构化事件日志）。
    /// Appends a pipeline event to the structured event log.
    /// </summary>
    /// <param name="evt">事件对象（不得为 null）/ Event object (must not be null).</param>
    void AddEvent(PipelineEvent evt);

    /// <summary>
    /// 本次运行的结构化事件日志（只读，按追加顺序）。
    /// The structured event log of this run (read-only, in append order).
    /// </summary>
    IReadOnlyList<PipelineEvent> Events { get; }
}
