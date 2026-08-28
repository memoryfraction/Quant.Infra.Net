using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Shared.Service;
using System.Threading.Channels;

namespace Quant.Infra.Net.Orchestration.Pipeline;

/// <summary>
/// 管道定时执行器（后台服务）：启动即执行第一轮，之后按 <see cref="IntervalTrigger"/> 周期性执行。
/// Periodic pipeline runner (background service): executes one cycle at startup, then on every trigger.
/// </summary>
/// <remarks>
/// 每一轮都使用全新的 <see cref="PipelineContext"/>（递增 runId）与当前 <see cref="OrchestrationOptions"/> 参数快照；
/// 单轮失败不终止宿主，异常经 ILogger 记录后继续等待下次触发。
/// Each round uses a fresh <see cref="PipelineContext"/> (incrementing run id) with the current <see cref="OrchestrationOptions"/> parameter snapshot;
/// a failed round is logged and does not stop the host.
/// </remarks>
public sealed class PipelineRunner : BackgroundService
{
    private readonly StrategyPipeline _pipeline;
    private readonly OrchestrationOptions _options;
    private readonly IntervalTrigger _trigger;
    private readonly ILogger _logger;
    private long _runId;

    /// <summary>
    /// 每轮执行完成后触发，携带本轮 <see cref="PipelineContext"/>（事件流与产物），供宿主观察或展示。
    /// Raised after every run completes with the run's <see cref="PipelineContext"/> (events and artifacts) for host-level inspection.
    /// </summary>
    public event Action<PipelineContext>? RunCompleted;

    /// <summary>
    /// 已完成轮数（用于测试与观测）。
    /// Completed run count (for testing and observability).
    /// </summary>
    public int CompletedRuns => (int)Interlocked.Read(ref _runId);

    /// <summary>
    /// 初始化管道执行器。
    /// Initializes the pipeline runner.
    /// </summary>
    /// <param name="pipeline">策略管道（不得为 null）/ Strategy pipeline (must not be null).</param>
    /// <param name="options">编排配置（不得为 null）/ Orchestration options (must not be null).</param>
    /// <param name="trigger">间隔触发器（不得为 null）/ Interval trigger (must not be null).</param>
    /// <param name="logger">日志器（可选，缺省为 NullLogger）/ Logger (optional; defaults to NullLogger).</param>
    /// <exception cref="ArgumentNullException">任一必填参数为 null 时抛出 / Thrown when a required argument is null.</exception>
    public PipelineRunner(
        StrategyPipeline pipeline,
        IOptions<OrchestrationOptions> options,
        IntervalTrigger trigger,
        ILogger<PipelineRunner>? logger = null)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _options = options.Value;
        _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        _logger = logger ?? new NullRunnerLogger();
    }

    /// <summary>
    /// 立即执行一轮管道（新 runId、新 context、当前参数快照），并触发 <see cref="RunCompleted"/>。
    /// Runs one pipeline cycle immediately (fresh run id / context / parameter snapshot) and raises <see cref="RunCompleted"/>.
    /// </summary>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>本轮执行后的管道上下文 / The pipeline context after the run.</returns>
    /// <exception cref="OperationCanceledException">取消令牌已取消时抛出 / Thrown when the token is canceled.</exception>
    public async Task<PipelineContext> RunOnceAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var runId = Interlocked.Increment(ref _runId);
        var context = new PipelineContext(runId, _options.Parameters);
        await _pipeline.RunAsync(context, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "pipeline run {RunId} completed: events={Events}, errors={Errors}",
            runId, context.Events.Count, context.Errors.Count);
        RunCompleted?.Invoke(context);
        return context;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        void OnTrigger(object? sender, EventArgs e) => channel.Writer.TryWrite(true);

        _trigger.IntervalTriggered += OnTrigger;
        try
        {
            _trigger.Start();
            _logger.LogInformation(
                "pipeline runner started: mode={Mode}, next trigger at {NextTrigger:u}",
                _trigger.Mode, _trigger.NextTriggerTime);

            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            while (!stoppingToken.IsCancellationRequested)
            {
                await channel.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 宿主优雅停机：正常结束 / graceful host shutdown: expected end
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "pipeline runner stopped with error");
        }
        finally
        {
            _trigger.IntervalTriggered -= OnTrigger;
            _trigger.Stop();
        }
    }

    /// <summary>
    /// 空的 ILogger 实现（宿主不提供日志器时的缺省回退）。
    /// Empty ILogger implementation (fallback when no logger is provided by the host).
    /// </summary>
    private sealed class NullRunnerLogger : ILogger<PipelineRunner>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
