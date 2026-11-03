using System.Globalization;
using System.Net;
using System.Text;
using Quant.Infra.Net.Backtest.Models;

namespace Quant.Infra.Net.Mcp.Reporting;

/// <summary>
/// 生成自包含的 HTML 回测报告（内联 SVG 图表，零外部依赖，可离线打开 / 发给别人）。
/// Writes a self-contained HTML backtest report (inline SVG charts, zero external dependencies; opens offline and
/// can be shared as a single file).
/// </summary>
public static class HtmlReportWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>报告目录：环境变量 QUANT_INFRA_REPORTS_DIR，否则 ~/Documents/QuantInfraNet/reports。
    /// Report directory: env QUANT_INFRA_REPORTS_DIR, else ~/Documents/QuantInfraNet/reports.</summary>
    public static string ReportsDir()
    {
        var env = Environment.GetEnvironmentVariable("QUANT_INFRA_REPORTS_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(docs)) docs = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(docs, "QuantInfraNet", "reports");
    }

    /// <summary>
    /// 写报告并返回完整路径 / Writes the report and returns its full path.
    /// </summary>
    public static string Write(string title, string subtitle, IReadOnlyDictionary<string, string> parameters,
        BacktestAnalysis a, BacktestResult r, string? extraSectionHtml = null)
    {
        var dir = ReportsDir();
        Directory.CreateDirectory(dir);
        var safe = string.Concat(title.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        if (safe.Length > 60) safe = safe[..60];
        var path = Path.Combine(dir, $"{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        File.WriteAllText(path, Render(title, subtitle, parameters, a, r, extraSectionHtml), Encoding.UTF8);
        return path;
    }

    /// <summary>渲染 HTML 字符串（公开以便单测）/ Renders the HTML string (public for tests).</summary>
    public static string Render(string title, string subtitle, IReadOnlyDictionary<string, string> parameters,
        BacktestAnalysis a, BacktestResult r, string? extraSectionHtml = null)
    {
        var s = a.Strategy;
        var b = a.Benchmark;
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>").Append(H(title)).Append("</title><style>").Append(Css).Append("</style></head><body><main>");
        sb.Append("<header><div class=\"brand\">Quant.Infra.Net · Backtest Report</div><h1>").Append(H(title)).Append("</h1><p class=\"sub\">")
          .Append(H(subtitle)).Append("</p></header>");

        // KPI tiles
        sb.Append("<section class=\"kpis\">");
        Kpi(sb, "CAGR", Pct(s.CagrPct), $"benchmark {Pct(b.CagrPct)}", s.CagrPct >= b.CagrPct);
        Kpi(sb, "Sharpe", Num(s.Sharpe), $"benchmark {Num(b.Sharpe)}", (s.Sharpe ?? -9) >= (b.Sharpe ?? -9));
        Kpi(sb, "Max drawdown", Pct(s.MaxDrawdownPct), $"benchmark {Pct(b.MaxDrawdownPct)}", s.MaxDrawdownPct >= b.MaxDrawdownPct);
        Kpi(sb, "Calmar", Num(s.Calmar), $"benchmark {Num(b.Calmar)}", (s.Calmar ?? -9) >= (b.Calmar ?? -9));
        Kpi(sb, "Total return", Pct(s.TotalReturnPct), $"benchmark {Pct(b.TotalReturnPct)}", s.TotalReturnPct >= b.TotalReturnPct);
        Kpi(sb, "Fills", r.Trades.Count.ToString(Inv), $"turnover {a.AnnualTurnover:0.0}×/yr", null);
        sb.Append("</section>");

        if (a.Warnings.Count > 0)
        {
            sb.Append("<section class=\"warn\"><h2>Read this first</h2><ul>");
            foreach (var w in a.Warnings) sb.Append("<li>").Append(H(w)).Append("</li>");
            sb.Append("</ul></section>");
        }

        sb.Append("<section><h2>Equity vs benchmark</h2><p class=\"note\">Benchmark: ").Append(H(a.BenchmarkLabel)).Append(". Both start at the same capital.</p>");
        sb.Append(LineChart(new[] { ("Strategy", "var(--s1)", a.Equity), ("Benchmark", "var(--s2)", a.BenchmarkEquity) }, v => "$" + Compact(v), false));
        sb.Append("</section>");

        sb.Append("<section><h2>Drawdown</h2>");
        sb.Append(LineChart(new[] { ("Drawdown", "var(--neg)", a.Drawdown) }, v => v.ToString("0", Inv) + "%", true));
        sb.Append("</section>");

        if (a.RollingSharpe.Count > 2)
        {
            sb.Append("<section><h2>Rolling 6-month Sharpe — is the edge decaying?</h2><p class=\"note\">Long stretches below 0 mean the strategy stopped working in that period (style / regime shift).</p>");
            sb.Append(LineChart(new[] { ("Rolling Sharpe", "var(--s1)", a.RollingSharpe) }, v => v.ToString("0.0", Inv), false, zeroLine: true));
            sb.Append("</section>");
        }

        // Regimes
        sb.Append("<section><h2>Market regimes — where does it win, where does it lose?</h2><p class=\"note\">")
          .Append(H(a.RegimeVerdict)).Append("</p><table><thead><tr><th>Regime</th><th>Meaning</th><th>% of days</th><th>Strategy ann. return</th><th>Strategy Sharpe</th><th>Benchmark ann. return</th><th>Benchmark Sharpe</th></tr></thead><tbody>");
        foreach (var g in a.Regimes)
        {
            sb.Append("<tr><td>").Append(H(g.Regime)).Append("</td><td>").Append(H(g.Description)).Append("</td><td>").Append(g.ShareOfDaysPct.ToString("0.#", Inv))
              .Append("%</td><td class=\"").Append(g.StrategyAnnReturnPct >= 0 ? "pos" : "neg").Append("\">").Append(Pct(g.StrategyAnnReturnPct))
              .Append("</td><td>").Append(Num(g.StrategySharpe)).Append("</td><td>").Append(Pct(g.BenchmarkAnnReturnPct)).Append("</td><td>").Append(Num(g.BenchmarkSharpe)).Append("</td></tr>");
        }

        sb.Append("</tbody></table></section>");

        // Yearly
        sb.Append("<section><h2>Calendar-year returns</h2>").Append(YearBars(a.Yearly)).Append("</section>");

        // Monthly heatmap
        sb.Append("<section><h2>Monthly returns (%)</h2><div class=\"scroll\"><table class=\"heat\"><thead><tr><th>Year</th>");
        foreach (var m in new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }) sb.Append("<th>").Append(m).Append("</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var (year, months) in a.Monthly)
        {
            sb.Append("<tr><td>").Append(year).Append("</td>");
            foreach (var v in months)
            {
                if (v is null)
                {
                    sb.Append("<td></td>");
                    continue;
                }

                var alpha = Math.Min(1.0, Math.Abs(v.Value) / 8.0) * 0.85 + 0.05;
                var color = v.Value >= 0 ? "var(--pos-rgb)" : "var(--neg-rgb)";
                sb.Append("<td style=\"background:rgba(").Append(color).Append(',').Append(alpha.ToString("0.00", Inv)).Append(")\">").Append(v.Value.ToString("0.0", Inv)).Append("</td>");
            }

            sb.Append("</tr>");
        }

        sb.Append("</tbody></table></div></section>");

        // Stats table
        sb.Append("<section><h2>Statistics</h2><table><thead><tr><th>Metric</th><th>Strategy</th><th>Benchmark</th></tr></thead><tbody>");
        Row(sb, "Total return", Pct(s.TotalReturnPct), Pct(b.TotalReturnPct));
        Row(sb, "CAGR", Pct(s.CagrPct), Pct(b.CagrPct));
        Row(sb, "Annual volatility", Pct(s.AnnVolPct), Pct(b.AnnVolPct));
        Row(sb, "Sharpe (rf=0)", Num(s.Sharpe), Num(b.Sharpe));
        Row(sb, "Sortino", Num(s.Sortino), Num(b.Sortino));
        Row(sb, "Max drawdown", Pct(s.MaxDrawdownPct), Pct(b.MaxDrawdownPct));
        Row(sb, "Calmar", Num(s.Calmar), Num(b.Calmar));
        Row(sb, "Best / worst day", $"{Pct(s.BestDayPct)} / {Pct(s.WorstDayPct)}", $"{Pct(b.BestDayPct)} / {Pct(b.WorstDayPct)}");
        Row(sb, "Positive months", Pct(s.PositiveMonthsPct), Pct(b.PositiveMonthsPct));
        Row(sb, "Beta / alpha (ann.)", $"{Num(a.Beta)} / {Pct(a.AlphaPct)}", "1.00 / 0%");
        Row(sb, "Correlation to benchmark", Num(a.Correlation), "1.00");
        Row(sb, "Time invested", Pct(a.ExposurePct), "100%");
        Row(sb, "Win rate / profit factor", $"{(r.Metrics.WinRate * 100).ToString("0.#", Inv)}% / {(double.IsFinite(r.Metrics.ProfitFactor) ? r.Metrics.ProfitFactor.ToString("0.00", Inv) : "∞")}", "—");
        Row(sb, "Commission paid", "$" + r.Metrics.TotalCommissionUsd.ToString("0.00", Inv), "—");
        sb.Append("</tbody></table></section>");

        if (!string.IsNullOrEmpty(extraSectionHtml)) sb.Append(extraSectionHtml);

        // Parameters
        sb.Append("<section><h2>Parameters</h2><table><tbody>");
        foreach (var (k, v) in parameters.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)) Row(sb, k, v);
        sb.Append("</tbody></table></section>");

        // Trades
        sb.Append("<section><h2>Fills (last 100)</h2><div class=\"scroll\"><table><thead><tr><th>Date</th><th>Symbol</th><th>Action</th><th>Price</th><th>Notional</th><th>Commission</th></tr></thead><tbody>");
        foreach (var t in r.Trades.OrderByDescending(t => t.TimestampUtc).Take(100))
        {
            sb.Append("<tr><td>").Append(t.TimestampUtc.ToString("yyyy-MM-dd", Inv)).Append("</td><td>").Append(H(t.Symbol)).Append("</td><td>")
              .Append(t.Side.ToString() == "Long" ? "Buy" : "Sell").Append("</td><td>").Append(t.FillPrice.ToString("0.####", Inv)).Append("</td><td>$")
              .Append(t.NotionalUsd.ToString("0", Inv)).Append("</td><td>$").Append(t.CommissionUsd.ToString("0.00", Inv)).Append("</td></tr>");
        }

        sb.Append("</tbody></table></div></section>");
        sb.Append("<footer>Generated ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm", Inv))
          .Append(" by Quant.Infra.Net MCP · github.com/memoryfraction/Quant.Infra.Net · Past performance does not predict future results. Not investment advice.</footer>");
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private static void Kpi(StringBuilder sb, string label, string value, string sub, bool? good)
    {
        sb.Append("<div class=\"kpi\"><div class=\"lbl\">").Append(H(label)).Append("</div><div class=\"val ")
          .Append(good is null ? "" : good.Value ? "pos" : "neg").Append("\">").Append(H(value)).Append("</div><div class=\"kpisub\">").Append(H(sub)).Append("</div></div>");
    }

    private static void Row(StringBuilder sb, params string[] cells)
    {
        sb.Append("<tr>");
        foreach (var c in cells) sb.Append("<td>").Append(H(c)).Append("</td>");
        sb.Append("</tr>");
    }

    /// <summary>多序列折线图（SVG）/ Multi-series line chart (SVG).</summary>
    public static string LineChart(IEnumerable<(string Name, string Color, IReadOnlyList<Point> Points)> series,
        Func<double, string> fmt, bool fillBelowZero, bool zeroLine = false)
    {
        var list = series.Where(x => x.Points.Count > 1).ToList();
        if (list.Count == 0) return "";
        const double W = 960, Hh = 300, L = 64, R = 12, T = 12, B = 28;
        var all = list.SelectMany(x => x.Points).ToList();
        var t0 = all.Min(p => p.T).Ticks;
        var t1 = Math.Max(t0 + 1, all.Max(p => p.T).Ticks);
        var vmin = all.Min(p => p.V);
        var vmax = all.Max(p => p.V);
        if (fillBelowZero) vmax = Math.Max(0, vmax);
        if (zeroLine) { vmin = Math.Min(0, vmin); vmax = Math.Max(0, vmax); }
        if (Math.Abs(vmax - vmin) < 1e-9) { vmax += 1; vmin -= 1; }
        var pad = (vmax - vmin) * 0.05;
        vmin -= fillBelowZero ? pad : pad;
        vmax += fillBelowZero ? 0 : pad;

        double X(DateTime t) => L + (t.Ticks - t0) / (double)(t1 - t0) * (W - L - R);
        double Y(double v) => T + (vmax - v) / (vmax - vmin) * (Hh - T - B);

        var sb = new StringBuilder();
        sb.Append("<div class=\"chart\"><svg viewBox=\"0 0 ").Append(W.ToString(Inv)).Append(' ').Append(Hh.ToString(Inv))
          .Append("\" role=\"img\">");
        for (var i = 0; i <= 4; i++)
        {
            var v = vmin + (vmax - vmin) * i / 4.0;
            var y = Y(v);
            sb.Append("<line x1=\"").Append(F(L)).Append("\" x2=\"").Append(F(W - R)).Append("\" y1=\"").Append(F(y)).Append("\" y2=\"").Append(F(y)).Append("\" class=\"grid\"/>");
            sb.Append("<text x=\"").Append(F(L - 6)).Append("\" y=\"").Append(F(y + 4)).Append("\" class=\"axis\" text-anchor=\"end\">").Append(H(fmt(v))).Append("</text>");
        }

        var startYear = new DateTime(t0).Year;
        var endYear = new DateTime(t1).Year;
        var stepY = Math.Max(1, (endYear - startYear + 1) / 10);
        if (endYear > startYear)
        {
            for (var yr = startYear + 1; yr <= endYear; yr += stepY)
            {
                var x = X(new DateTime(yr, 1, 1));
                sb.Append("<text x=\"").Append(F(x)).Append("\" y=\"").Append(F(Hh - 8)).Append("\" class=\"axis\" text-anchor=\"middle\">").Append(yr).Append("</text>");
            }
        }
        else
        {
            for (var i = 0; i <= 4; i++)
            {
                var t = new DateTime(t0 + (t1 - t0) * i / 4);
                sb.Append("<text x=\"").Append(F(X(t))).Append("\" y=\"").Append(F(Hh - 8)).Append("\" class=\"axis\" text-anchor=\"middle\">").Append(t.ToString("MMM dd", Inv)).Append("</text>");
            }
        }

        if (zeroLine)
        {
            sb.Append("<line x1=\"").Append(F(L)).Append("\" x2=\"").Append(F(W - R)).Append("\" y1=\"").Append(F(Y(0))).Append("\" y2=\"").Append(F(Y(0))).Append("\" class=\"zero\"/>");
        }

        foreach (var (name, color, pts) in list)
        {
            var step = Math.Max(1, pts.Count / 1200);
            var d = new StringBuilder();
            for (var i = 0; i < pts.Count; i += step)
            {
                d.Append(i == 0 ? 'M' : 'L').Append(F(X(pts[i].T))).Append(',').Append(F(Y(pts[i].V)));
            }

            d.Append('L').Append(F(X(pts[^1].T))).Append(',').Append(F(Y(pts[^1].V)));
            if (fillBelowZero)
            {
                sb.Append("<path d=\"").Append(d).Append('L').Append(F(X(pts[^1].T))).Append(',').Append(F(Y(0))).Append('L').Append(F(X(pts[0].T))).Append(',').Append(F(Y(0)))
                  .Append("Z\" fill=\"").Append(color).Append("\" fill-opacity=\"0.25\" stroke=\"none\"/>");
            }

            sb.Append("<path d=\"").Append(d).Append("\" fill=\"none\" stroke=\"").Append(color).Append("\" stroke-width=\"1.6\" vector-effect=\"non-scaling-stroke\"><title>").Append(H(name)).Append("</title></path>");
        }

        sb.Append("</svg>");
        if (list.Count > 1)
        {
            sb.Append("<div class=\"legend\">");
            foreach (var (name, color, _) in list) sb.Append("<span><i style=\"background:").Append(color).Append("\"></i>").Append(H(name)).Append("</span>");
            sb.Append("</div>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string YearBars(IReadOnlyList<YearRow> rows)
    {
        if (rows.Count == 0) return "";
        const double W = 960, Hh = 260, L = 48, R = 12, T = 12, B = 28;
        var vmax = Math.Max(1, rows.Max(r => Math.Max(r.StrategyPct, r.BenchmarkPct)));
        var vmin = Math.Min(0, rows.Min(r => Math.Min(r.StrategyPct, r.BenchmarkPct)));
        double Y(double v) => T + (vmax - v) / (vmax - vmin) * (Hh - T - B);
        var slot = (W - L - R) / rows.Count;
        var bw = Math.Min(28, slot * 0.38);
        var sb = new StringBuilder("<div class=\"chart\"><svg viewBox=\"0 0 960 260\" role=\"img\">");
        sb.Append("<line x1=\"").Append(F(L)).Append("\" x2=\"").Append(F(W - R)).Append("\" y1=\"").Append(F(Y(0))).Append("\" y2=\"").Append(F(Y(0))).Append("\" class=\"zero\"/>");
        for (var i = 0; i < rows.Count; i++)
        {
            var cx = L + slot * (i + 0.5);
            Bar(sb, cx - bw - 1, bw, Y(0), Y(rows[i].StrategyPct), "var(--s1)", $"{rows[i].Year} strategy {rows[i].StrategyPct:0.#}%");
            Bar(sb, cx + 1, bw, Y(0), Y(rows[i].BenchmarkPct), "var(--s2)", $"{rows[i].Year} benchmark {rows[i].BenchmarkPct:0.#}%");
            if (rows.Count <= 20 || i % 2 == 0)
                sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(Hh - 8)).Append("\" class=\"axis\" text-anchor=\"middle\">").Append(rows[i].Year).Append("</text>");
        }

        sb.Append("</svg><div class=\"legend\"><span><i style=\"background:var(--s1)\"></i>Strategy</span><span><i style=\"background:var(--s2)\"></i>Benchmark</span></div></div>");
        return sb.ToString();
    }

    private static void Bar(StringBuilder sb, double x, double w, double y0, double y1, string color, string title)
    {
        var top = Math.Min(y0, y1);
        var h = Math.Max(1, Math.Abs(y1 - y0));
        sb.Append("<rect x=\"").Append(F(x)).Append("\" y=\"").Append(F(top)).Append("\" width=\"").Append(F(w)).Append("\" height=\"").Append(F(h))
          .Append("\" fill=\"").Append(color).Append("\" rx=\"2\"><title>").Append(H(title)).Append("</title></rect>");
    }

    private static string F(double v) => v.ToString("0.#", Inv);
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string Pct(double? v) => v is null ? "—" : v.Value.ToString("0.##", Inv) + "%";
    private static string Num(double? v) => v is null ? "—" : v.Value.ToString("0.00", Inv);

    private static string Compact(double v)
        => Math.Abs(v) >= 1e6 ? (v / 1e6).ToString("0.##", Inv) + "M" : Math.Abs(v) >= 1e3 ? (v / 1e3).ToString("0.#", Inv) + "k" : v.ToString("0", Inv);

    private const string Css = """
:root{--bg:#fbfaf8;--card:#ffffff;--fg:#1d1d1f;--muted:#6b6b70;--line:#e7e5e1;--s1:#2f6fdb;--s2:#a0a0a8;--pos:#1a8a5a;--neg:#c8463d;--pos-rgb:26,138,90;--neg-rgb:200,70,61;--warn:#fff6e0;--warnline:#e9c46a}
@media (prefers-color-scheme:dark){:root{--bg:#151517;--card:#1e1e21;--fg:#ececee;--muted:#9a9aa2;--line:#2e2e33;--s1:#6ea0ff;--s2:#7c7c86;--pos:#4cc38a;--neg:#ff7a6e;--pos-rgb:76,195,138;--neg-rgb:255,122,110;--warn:#2a2416;--warnline:#8a6d2a}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.5 -apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Noto Sans SC",sans-serif}
main{max-width:1040px;margin:0 auto;padding:28px 16px 48px}header{margin-bottom:20px}.brand{font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:var(--muted)}
h1{font-size:26px;margin:4px 0}h2{font-size:17px;margin:0 0 10px}.sub,.note{color:var(--muted);margin:0 0 10px}
section{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:18px;margin:14px 0}
.kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:10px;background:none;border:0;padding:0}
.kpi{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px}.lbl{font-size:12px;color:var(--muted)}.val{font-size:24px;font-weight:650;font-variant-numeric:tabular-nums}.kpisub{font-size:12px;color:var(--muted)}
.pos{color:var(--pos)}.neg{color:var(--neg)}.warn{background:var(--warn);border-color:var(--warnline)}.warn ul{margin:0;padding-left:18px}
table{width:100%;border-collapse:collapse;font-variant-numeric:tabular-nums;font-size:14px}th,td{padding:6px 8px;border-bottom:1px solid var(--line);text-align:left}th{color:var(--muted);font-weight:600}
.heat td{text-align:center;min-width:44px}.scroll{overflow-x:auto}.chart svg{width:100%;height:auto;display:block}.grid{stroke:var(--line)}.zero{stroke:var(--muted);stroke-dasharray:3 3}
.axis{fill:var(--muted);font-size:11px}.legend{display:flex;gap:16px;font-size:13px;color:var(--muted);margin-top:6px}.legend i{display:inline-block;width:12px;height:3px;margin-right:6px;vertical-align:middle}
footer{color:var(--muted);font-size:12px;margin-top:18px}
""";
}
