using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Orchestration.Stages;

/// <summary>
/// 从 PipelineContext 已加载行情中提取每个标的的最新收盘价（供 Paper 服务估值）。
/// Extracts each symbol's latest close from context-loaded market data (for Paper valuation).
/// </summary>
internal static class StageMarketData
{
    /// <summary>
    /// 提取最新收盘价表（单 Ohlcvs 槽 + 合并 HashSet 槽）。
    /// Extracts latest closes (single Ohlcvs slot + merged HashSet slot).
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <returns>symbol 到最新收盘价 / Symbol to latest close.</returns>
    public static IReadOnlyDictionary<string, double> ExtractLatestCloses(IPipelineContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var bestTime = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var single = context.Get<Ohlcvs>();
        if (single?.OhlcvSet != null)
        {
            foreach (var ohlcv in single.OhlcvSet)
            {
                Upsert(ohlcv, bestTime, result);
            }
        }

        var merged = context.Get<HashSet<Ohlcv>>();
        if (merged != null)
        {
            foreach (var ohlcv in merged)
            {
                Upsert(ohlcv, bestTime, result);
            }
        }

        return result;
    }

    /// <summary>
    /// 将 Paper 标记价写入支持 <see cref="IPaperMarkable"/> 的券商实现（不支持时静默跳过）。
    /// Applies Paper mark prices to brokers implementing <see cref="IPaperMarkable"/> (silently skips otherwise).
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="broker">券商无关的执行接口 / Broker-agnostic execution surface.</param>
    public static void ApplyPaperMarks(IPipelineContext context, IExecutionBroker broker)
    {
        if (broker is not IPaperMarkable markable)
        {
            return;
        }

        var latest = ExtractLatestCloses(context);
        if (latest.Count > 0)
        {
            markable.SetMarkPrices(latest);
        }
    }

    private static void Upsert(Ohlcv? ohlcv, Dictionary<string, DateTime> bestTime, Dictionary<string, double> sink)
    {
        if (ohlcv == null || string.IsNullOrWhiteSpace(ohlcv.Symbol) || ohlcv.Close <= 0m)
        {
            return;
        }

        var key = ohlcv.Symbol;
        if (!bestTime.TryGetValue(key, out var existing) || ohlcv.OpenDateTime >= existing)
        {
            bestTime[key] = ohlcv.OpenDateTime;
            sink[key] = (double)ohlcv.Close;
        }
    }
}
