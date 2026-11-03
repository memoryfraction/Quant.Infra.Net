using System.Globalization;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Runtime.Strategies;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Mcp.Strategies;

/// <summary>
/// 多因子轮动策略描述符（被 StrategyCatalog 反射发现，名字 <c>MultiFactor</c>）。
/// Multi-factor rotation strategy descriptor (discovered by StrategyCatalog via reflection; name <c>MultiFactor</c>).
/// </summary>
public sealed class MultiFactorRotationDescriptor : IStrategyDescriptor
{
    /// <inheritdoc />
    public string Name => MultiFactorRotation.GeneratorId;

    /// <inheritdoc />
    public ISignalGenerator Create(IServiceProvider serviceProvider) => new MultiFactorRotation();
}

/// <summary>
/// 多因子横截面轮动：在一篮子标的里，按多个因子的 z-score 加权打分，持有得分最高的 TopK 只，等权，每 RebalanceDays 根 K 线调仓。
/// 可选：绝对动量过滤（动量 ≤ 0 不持有）、市场状态过滤（RegimeSymbol 跌破均线 → 空仓或切到防御资产）。
/// Cross-sectional multi-factor rotation: scores a basket by a weighted sum of per-factor z-scores, holds the TopK
/// best equal-weight, rebalancing every RebalanceDays bars. Optional absolute-momentum filter (skip names with
/// momentum ≤ 0) and regime filter (RegimeSymbol below its SMA → cash or DefensiveSymbol).
/// </summary>
/// <remarks>
/// <para>因子 / Factors (Factors 参数，如 "momentum=1,lowvol=0.5")：</para>
/// <list type="bullet">
///   <item><c>momentum</c>：过去 MomentumLookback 根收益（跳过最近 SkipRecent 根）/ trailing return, skipping the most recent bars</item>
///   <item><c>lowvol</c>：−日收益波动率（VolLookback）/ negative daily-return volatility</item>
///   <item><c>trend</c>：收盘价 / SMA(TrendPeriod) − 1 / distance above the trend SMA</item>
///   <item><c>reversal</c>：−最近 5 根收益（短期反转）/ negative 5-bar return (short-term reversal)</item>
///   <item><c>riskadjmom</c>：动量 / 波动率（类夏普动量）/ momentum divided by volatility</item>
/// </list>
/// <para>只用截至当前 bar 的数据（回测引擎注入 as-of 切片），无未来函数。/ Uses only as-of data (the engine injects
/// as-of slices) — no look-ahead.</para>
/// </remarks>
public sealed class MultiFactorRotation : ISignalGenerator
{
    /// <summary>策略名 / Strategy name.</summary>
    public const string GeneratorId = "MultiFactor";

    /// <summary>支持的因子名 / Supported factor names.</summary>
    public static readonly string[] FactorNames = { "momentum", "lowvol", "trend", "reversal", "riskadjmom" };

    private int _barsSinceRebalance = int.MaxValue;
    private List<string> _holdings = new();

    /// <inheritdoc />
    public string Id => GeneratorId;

    /// <summary>
    /// 该参数组合所需的最少历史 bar 数（引擎据此自动预取预热数据）。
    /// Minimum history bars needed by these parameters (the engine pre-fetches warm-up history accordingly).
    /// </summary>
    public static int RequiredBars(Func<string, string?> p)
    {
        var o = Options.From(p);
        var need = Math.Max(o.MomentumLookback + o.SkipRecent, o.VolLookback + 1);
        if (o.Weights.ContainsKey("trend")) need = Math.Max(need, o.TrendPeriod);
        if (!string.IsNullOrWhiteSpace(o.RegimeSymbol)) need = Math.Max(need, o.RegimeSmaPeriod);
        return need + 1;
    }

    /// <summary>参与回测需要加载数据的全部符号 / All symbols whose data the backtest must load.</summary>
    public static IReadOnlyList<string> AllSymbols(Func<string, string?> p)
    {
        var o = Options.From(p);
        var list = new List<string>(o.Universe);
        if (!string.IsNullOrWhiteSpace(o.RegimeSymbol) && !list.Contains(o.RegimeSymbol, StringComparer.OrdinalIgnoreCase)) list.Add(o.RegimeSymbol!);
        if (!string.IsNullOrWhiteSpace(o.DefensiveSymbol) && !list.Contains(o.DefensiveSymbol, StringComparer.OrdinalIgnoreCase)) list.Add(o.DefensiveSymbol!);
        return list;
    }

    /// <summary>实际 TopK（0 = 自动取 universe 的 1/3）/ Effective TopK (0 = auto, a third of the universe).</summary>
    public static int EffectiveTopK(Func<string, string?> p) => Options.From(p).TopK;

    /// <inheritdoc />
    public Task<IReadOnlyList<Signal>> GenerateSignalsAsync(IPipelineContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var o = Options.From(context.GetParameter);
        if (o.Universe.Count < 2)
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, Id, "rejected: Universe needs at least 2 symbols, e.g. Universe=SPY,QQQ,IWM,EFA,TLT,GLD"));
            return Task.FromResult<IReadOnlyList<Signal>>(Array.Empty<Signal>());
        }

        var union = context.Get<HashSet<Ohlcv>>();
        if (union is null || union.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<Signal>>(Array.Empty<Signal>());
        }

        _barsSinceRebalance = _barsSinceRebalance == int.MaxValue ? int.MaxValue : _barsSinceRebalance + 1;
        if (_barsSinceRebalance >= o.RebalanceDays)
        {
            // 只在调仓日分组排序（非调仓日直接沿用持仓），把每根 bar 的开销从 O(全部历史) 降到 O(标的数)。
            // Group/sort only on rebalance days (other bars re-emit holdings), cutting per-bar cost.
            var closes = union
                .GroupBy(b => b.Symbol, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderBy(b => b.OpenDateTime).Select(b => (double)b.Close).ToList(), StringComparer.OrdinalIgnoreCase);
            Rebalance(context, o, closes);
        }

        var tradable = AllSymbols(context.GetParameter);
        var now = DateTime.UtcNow;
        var signals = tradable.Select(s => new Signal
        {
            Symbol = s,
            GeneratedUtc = now,
            Direction = _holdings.Contains(s, StringComparer.OrdinalIgnoreCase) ? SignalDirection.Long : SignalDirection.Flat,
            Strength = 1.0,
            Reason = _holdings.Contains(s, StringComparer.OrdinalIgnoreCase) ? "multi-factor pick" : "not selected"
        }).ToList();

        return Task.FromResult<IReadOnlyList<Signal>>(signals);
    }

    private void Rebalance(IPipelineContext context, Options o, Dictionary<string, List<double>> closes)
    {
        var need = RequiredBars(context.GetParameter);
        var eligible = o.Universe.Where(s => closes.TryGetValue(s, out var c) && c.Count >= need).ToList();
        if (eligible.Count < 2)
        {
            // 数据不足以打分：保持现状（不计入调仓），下一根再试。/ Not enough data to score yet: keep state, retry next bar.
            return;
        }

        _barsSinceRebalance = 0;

        // 市场状态过滤 / Regime filter
        if (!string.IsNullOrWhiteSpace(o.RegimeSymbol) && closes.TryGetValue(o.RegimeSymbol!, out var rc) && rc.Count >= o.RegimeSmaPeriod)
        {
            var sma = rc.TakeLast(o.RegimeSmaPeriod).Average();
            if (rc[^1] < sma)
            {
                _holdings = string.IsNullOrWhiteSpace(o.DefensiveSymbol) ? new List<string>() : new List<string> { o.DefensiveSymbol! };
                context.AddEvent(PipelineEvent.Create(context.RunId, Id, string.Format(CultureInfo.InvariantCulture,
                    "regime OFF: {0} {1:0.00} < SMA{2} {3:0.00} → {4}", o.RegimeSymbol, rc[^1], o.RegimeSmaPeriod, sma,
                    _holdings.Count == 0 ? "cash" : o.DefensiveSymbol)));
                return;
            }
        }

        var raw = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in eligible)
        {
            raw[s] = ComputeFactors(closes[s], o);
        }

        var score = eligible.ToDictionary(s => s, _ => 0.0, StringComparer.OrdinalIgnoreCase);
        foreach (var (factor, weight) in o.Weights)
        {
            var values = eligible.Select(s => raw[s][factor]).ToList();
            var mean = values.Average();
            var sd = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
            foreach (var s in eligible)
            {
                var z = sd > 1e-12 ? (raw[s][factor] - mean) / sd : 0.0;
                score[s] += weight * z;
            }
        }

        var ranked = eligible.OrderByDescending(s => score[s]).ToList();
        var picks = ranked.Take(o.TopK).ToList();
        if (o.AbsMomentumFilter)
        {
            picks = picks.Where(s => raw[s]["momentum"] > 0).ToList();
        }

        if (picks.Count == 0 && !string.IsNullOrWhiteSpace(o.DefensiveSymbol))
        {
            picks.Add(o.DefensiveSymbol!);
        }

        _holdings = picks;
        context.AddEvent(PipelineEvent.Create(context.RunId, Id, "rebalance → " +
            (picks.Count == 0 ? "cash (no positive momentum)" : string.Join(", ", picks)) + " | ranking: " +
            string.Join(" > ", ranked.Select(s => string.Format(CultureInfo.InvariantCulture, "{0}({1:0.00})", s, score[s])))));
    }

    /// <summary>
    /// 计算单个标的的全部因子原始值（公开以便单测）/ Computes all raw factor values for one symbol (public for tests).
    /// </summary>
    public static Dictionary<string, double> ComputeFactors(IReadOnlyList<double> c, Options o)
    {
        var n = c.Count;
        var end = n - 1 - o.SkipRecent;
        var begin = end - o.MomentumLookback;
        var momentum = begin >= 0 && c[begin] > 0 ? c[end] / c[begin] - 1.0 : 0.0;

        var rets = new List<double>();
        for (var i = Math.Max(1, n - o.VolLookback); i < n; i++)
        {
            if (c[i - 1] > 0) rets.Add(c[i] / c[i - 1] - 1.0);
        }

        var mean = rets.Count > 0 ? rets.Average() : 0.0;
        var vol = rets.Count > 1 ? Math.Sqrt(rets.Sum(r => (r - mean) * (r - mean)) / (rets.Count - 1)) : 0.0;
        var trendN = Math.Min(o.TrendPeriod, n);
        var sma = c.Skip(n - trendN).Average();
        var rev = n > 5 && c[n - 6] > 0 ? -(c[n - 1] / c[n - 6] - 1.0) : 0.0;
        var annVolOverHorizon = vol * Math.Sqrt(Math.Max(1, o.MomentumLookback));

        return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["momentum"] = momentum,
            ["lowvol"] = -vol,
            ["trend"] = sma > 0 ? c[n - 1] / sma - 1.0 : 0.0,
            ["reversal"] = rev,
            ["riskadjmom"] = annVolOverHorizon > 1e-12 ? momentum / annVolOverHorizon : 0.0
        };
    }

    /// <summary>
    /// 解析后的策略参数 / Parsed strategy parameters.
    /// </summary>
    public sealed class Options
    {
        /// <summary>候选池 / Universe.</summary>
        public List<string> Universe { get; init; } = new();
        /// <summary>因子权重 / Factor weights.</summary>
        public Dictionary<string, double> Weights { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>持有数 / Number of holdings.</summary>
        public int TopK { get; init; }
        /// <summary>调仓间隔（bar）/ Rebalance interval in bars.</summary>
        public int RebalanceDays { get; init; }
        /// <summary>动量回看 / Momentum lookback.</summary>
        public int MomentumLookback { get; init; }
        /// <summary>跳过最近 N 根 / Skip the most recent N bars.</summary>
        public int SkipRecent { get; init; }
        /// <summary>波动率回看 / Volatility lookback.</summary>
        public int VolLookback { get; init; }
        /// <summary>趋势均线 / Trend SMA period.</summary>
        public int TrendPeriod { get; init; }
        /// <summary>绝对动量过滤 / Absolute momentum filter.</summary>
        public bool AbsMomentumFilter { get; init; }
        /// <summary>市场状态参考标的 / Regime reference symbol.</summary>
        public string? RegimeSymbol { get; init; }
        /// <summary>市场状态均线 / Regime SMA period.</summary>
        public int RegimeSmaPeriod { get; init; }
        /// <summary>防御资产 / Defensive asset.</summary>
        public string? DefensiveSymbol { get; init; }

        /// <summary>从参数查询函数解析 / Parses from a parameter lookup.</summary>
        public static Options From(Func<string, string?> p)
        {
            var universe = (p("Universe") ?? "")
                .Split(new[] { ',', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpperInvariant()).Distinct().ToList();

            var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in (p("Factors") ?? "momentum=1").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = part.Split(new[] { '=', ':' }, 2, StringSplitOptions.TrimEntries);
                var name = kv[0].ToLowerInvariant();
                if (!FactorNames.Contains(name))
                {
                    throw new ArgumentException($"Unknown factor '{kv[0]}'. Supported: {string.Join(", ", FactorNames)}. Example: Factors=momentum=1,lowvol=0.5");
                }

                weights[name] = kv.Length > 1 && double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 1.0;
            }

            int I(string k, int d) => int.TryParse(p(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : d;
            var topK = I("TopK", 0);
            if (topK <= 0) topK = Math.Max(1, (int)Math.Round(universe.Count / 3.0));
            var abs = p("AbsMomentumFilter");

            return new Options
            {
                Universe = universe,
                Weights = weights,
                TopK = Math.Min(topK, Math.Max(1, universe.Count)),
                RebalanceDays = I("RebalanceDays", 21),
                MomentumLookback = I("MomentumLookback", 126),
                SkipRecent = int.TryParse(p("SkipRecent"), out var sr) && sr >= 0 ? sr : 0,
                VolLookback = I("VolLookback", 63),
                TrendPeriod = I("TrendPeriod", 200),
                AbsMomentumFilter = abs is null || !abs.Equals("false", StringComparison.OrdinalIgnoreCase),
                RegimeSymbol = string.IsNullOrWhiteSpace(p("RegimeSymbol")) ? null : p("RegimeSymbol")!.Trim().ToUpperInvariant(),
                RegimeSmaPeriod = I("RegimeSmaPeriod", 200),
                DefensiveSymbol = string.IsNullOrWhiteSpace(p("DefensiveSymbol")) ? null : p("DefensiveSymbol")!.Trim().ToUpperInvariant()
            };
        }
    }
}
