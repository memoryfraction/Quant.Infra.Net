namespace Quant.Infra.Net.Orchestration.Signals;

/// <summary>
/// 通用数值工具（确定性、无外部依赖）。
/// General numeric utilities (deterministic, no external dependencies).
/// </summary>
public static class OrchestrationNumerics
{
    /// <summary>
    /// 总体标准差（population std dev）；空序列返回 0。
    /// Population standard deviation; returns 0 for an empty series.
    /// </summary>
    /// <param name="values">数值序列 / Values.</param>
    /// <returns>标准差 / Standard deviation.</returns>
    public static double PopulationStdDev(IReadOnlyList<double> values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Count == 0)
        {
            return 0.0;
        }

        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }
}
