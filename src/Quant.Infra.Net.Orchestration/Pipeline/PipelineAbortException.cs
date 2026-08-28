namespace Quant.Infra.Net.Orchestration.Pipeline;

/// <summary>
/// 业务性终止管道的异常（如风控拒绝）：不代表系统故障，Runner 不将其视为致命错误。
/// Business-level pipeline termination exception (e.g., risk rejection): not a system fault; the runner does not treat it as fatal.
/// </summary>
public class PipelineAbortException : Exception
{
    /// <summary>
    /// 创建管道终止异常。
    /// Creates a pipeline abort exception.
    /// </summary>
    /// <param name="reason">终止原因（不得为空白）/ Abort reason (must not be blank).</param>
    /// <param name="inner">内部异常（可选）/ Inner exception (optional).</param>
    /// <exception cref="ArgumentException">reason 为空白时抛出 / Thrown when reason is blank.</exception>
    public PipelineAbortException(string reason, Exception? inner = null)
        : base(string.IsNullOrWhiteSpace(reason) ? "Pipeline aborted." : reason, inner)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Abort reason must not be blank.", nameof(reason));
        }
    }
}
