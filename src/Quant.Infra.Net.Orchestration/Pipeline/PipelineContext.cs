using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Pipeline;

/// <summary>
/// 管道上下文实现：类型化数据槽 + 有序事件日志 + 错误累积；线程安全。
/// Pipeline context implementation: typed data slots + ordered event log + error accumulation; thread-safe.
/// </summary>
public sealed class PipelineContext : IPipelineContext
{
    private readonly object _gate = new();

    private readonly Dictionary<Type, object?> _slots = new();

    private readonly List<Exception> _errors = new();

    private readonly List<PipelineEvent> _events = new();

    /// <summary>
    /// 构造函数为内部使用；宿主通过构造函数注入参数表。
    /// Internal-use constructor; the host supplies the parameter table.
    /// </summary>
    /// <param name="runId">运行 ID / Run id.</param>
    /// <param name="parameters">策略参数表（可选，键不得为空白）/ Strategy parameters (optional; keys must not be blank).</param>
    /// <exception cref="ArgumentException">参数键为空时抛出 / Thrown when a parameter key is blank.</exception>
    public PipelineContext(long runId, IReadOnlyDictionary<string, string>? parameters = null)
    {
        RunId = runId;

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (parameters != null)
        {
            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new ArgumentException("Parameter keys must not be blank.", nameof(parameters));
                }

                dict[key] = value;
            }
        }

        Parameters = dict;
    }

    /// <inheritdoc />
    public long RunId { get; }

    /// <summary>
    /// 只读策略参数表（键比较忽略大小写）。
    /// Read-only strategy parameter table (case-insensitive keys).
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <inheritdoc />
    public string? GetParameter(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Parameter key must not be blank.", nameof(key));
        }

        lock (_gate)
        {
            return Parameters.TryGetValue(key, out var value) ? value : null;
        }
    }

    /// <inheritdoc />
    public void Set<T>(T value) where T : class
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        lock (_gate)
        {
            _slots[typeof(T)] = value;
        }
    }

    /// <inheritdoc />
    public T? Get<T>() where T : class
    {
        lock (_gate)
        {
            return _slots.TryGetValue(typeof(T), out var value) ? (T?)value : null;
        }
    }

    /// <inheritdoc />
    public void AddError(Exception error)
    {
        if (error == null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        lock (_gate)
        {
            _events.Add(PipelineEvent.Create(RunId, "Pipeline", $"error accumulated: {error.Message}", severity: NotificationSeverity.Critical));
            _errors.Add(error);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Exception> Errors
    {
        get
        {
            lock (_gate)
            {
                return _errors.ToList();
            }
        }
    }

    /// <inheritdoc />
    public void AddEvent(PipelineEvent evt)
    {
        if (evt == null)
        {
            throw new ArgumentNullException(nameof(evt));
        }

        lock (_gate)
        {
            _events.Add(evt);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PipelineEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToList();
            }
        }
    }
}
