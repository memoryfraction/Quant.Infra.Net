using System.Globalization;
using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Signals;

/// <summary>
/// 信号参数解析（InvariantCulture，容错回退缺省值）。
/// Signal parameter parsing (InvariantCulture, fault-tolerant fallbacks to defaults).
/// </summary>
internal static class SignalParams
{
    /// <summary>
    /// 读取整型参数。Reads an integer parameter.</summary>
    /// <param name="context">上下文 / Context.</param>
    /// <param name="key">参数名 / Parameter name.</param>
    /// <param name="defaultValue">缺省值 / Default.</param>
    /// <returns>参数值 / Parsed value.</returns>
    public static int GetInt(IPipelineContext context, string key, int defaultValue)
        => int.TryParse(context.GetParameter(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

    /// <summary>
    /// 读取双精度参数。Reads a double parameter.</summary>
    /// <param name="context">上下文 / Context.</param>
    /// <param name="key">参数名 / Parameter name.</param>
    /// <param name="defaultValue">缺省值 / Default.</param>
    /// <returns>参数值 / Parsed value.</returns>
    public static double GetDouble(IPipelineContext context, string key, double defaultValue)
        => double.TryParse(context.GetParameter(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

    /// <summary>
    /// 读取布尔参数。Reads a boolean parameter.</summary>
    /// <param name="context">上下文 / Context.</param>
    /// <param name="key">参数名 / Parameter name.</param>
    /// <param name="defaultValue">缺省值 / Default.</param>
    /// <returns>参数值 / Parsed value.</returns>
    public static bool GetBool(IPipelineContext context, string key, bool defaultValue)
        => bool.TryParse(context.GetParameter(key), out var v) ? v : defaultValue;

    /// <summary>
    /// 读取数据源参数（"yahoo" | "binance"，非法值回退 yahoo）。
    /// Reads the data source parameter ("yahoo" | "binance"; invalid values fall back to yahoo).</summary>
    /// <param name="context">上下文 / Context.</param>
    /// <returns>小写数据源名 / Lower-cased source name.</returns>
    public static string GetDataSource(IPipelineContext context)
    {
        var raw = (context.GetParameter("DataSource") ?? "yahoo").Trim().ToLowerInvariant();
        return raw is "yahoo" or "binance" ? raw : "yahoo";
    }

    /// <summary>
    /// 解析 K 线周期限定（非法/缺省时按数据源回退：yahoo=Daily, binance=Hourly）。
    /// Parses the resolution level (falls back to yahoo=Daily / binance=Hourly when blank or invalid).</summary>
    /// <param name="context">上下文 / Context.</param>
    /// <returns>K 线级别 / Resolution level.</returns>
    public static Quant.Infra.Net.Shared.Model.ResolutionLevel ParseResolution(IPipelineContext context)
    {
        var raw = context.GetParameter("ResolutionLevel");
        if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse<Quant.Infra.Net.Shared.Model.ResolutionLevel>(raw, true, out var level))
        {
            return level;
        }

        return GetDataSource(context) == "binance"
            ? Quant.Infra.Net.Shared.Model.ResolutionLevel.Hourly
            : Quant.Infra.Net.Shared.Model.ResolutionLevel.Daily;
    }

    /// <summary>
    /// 序列最小可用长度：max(10, bars/10)。
    /// Minimum usable series length: max(10, bars/10).</summary>
    /// <param name="bars">目标 bar 数 / Target bar count.</param>
    /// <returns>最小长度 / Minimum length.</returns>
    public static int MinBars(int bars) => Math.Max(10, Math.Max(1, bars) / 10);
}


