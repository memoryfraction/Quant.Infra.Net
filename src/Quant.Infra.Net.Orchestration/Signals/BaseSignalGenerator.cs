using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration.Signals;

/// <summary>
/// 信号生成器基类：分析服务持有 + 数据装载规则（委托 <see cref="SignalDataLoader"/>）+ 参数解析。
/// Signal generator base class: holds the analysis service, delegates the data-loading rule to <see cref="SignalDataLoader"/>, and parses parameters.
/// </summary>
/// <remarks>
/// 数据装载规则（按序，见 <see cref="SignalDataLoader"/>）：
/// 1) context 中已存的 <see cref="Ohlcvs"/>（Symbol 匹配）；
/// 2) context 中的 <see cref="HashSet{T}"/> 合并槽（按 Symbol 过滤）；
/// 3) 按参数 DataSource（"yahoo" | "binance"）直接拉取（Yahoo = 只读行情接口；binance 仅 GetOhlcvListAsync 只读，Paper 安全）。
/// 任一步无数据都返回空序列（不抛业务异常），由生成器记录"数据不足"事件。
/// Data loading rule (see SignalDataLoader): cached Ohlcvs → merged slot → direct fetch. Missing data yields an empty
/// close series (no business exception); the generator records an "insufficient data" event.
/// </remarks>
public abstract class BaseSignalGenerator : ISignalGenerator
{
    /// <summary>
    /// 初始化基类。
    /// Initializes the base class.
    /// </summary>
    /// <param name="analysisService">分析服务（不得为 null）/ Analysis service (must not be null).</param>
    /// <param name="yahooData">Yahoo/传统金融数据源（可选）/ Yahoo/traditional-finance data source (optional).</param>
    /// <param name="binanceService">币安合约服务（可选，Paper 下为纯内存实现）/ Binance futures service (optional; pure in-memory under Paper).</param>
    /// <exception cref="ArgumentNullException">analysisService 为 null 时抛出 / Thrown when analysisService is null.</exception>
    protected BaseSignalGenerator(
        IAnalysisService analysisService,
        ITraditionalFinanceSourceDataService? yahooData = null,
        IBinanceUsdFutureService? binanceService = null)
    {
        AnalysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
        YahooData = yahooData;
        BinanceService = binanceService;
    }

    /// <summary>
    /// 分析服务（只读）。
    /// Analysis service (read-only).
    /// </summary>
    protected IAnalysisService AnalysisService { get; }

    /// <summary>
    /// Yahoo/传统金融数据源（只读，可为 null）。
    /// Yahoo/traditional-finance data source (read-only, may be null).
    /// </summary>
    protected ITraditionalFinanceSourceDataService? YahooData { get; }

    /// <summary>
    /// 币安合约服务（只读，可为 null）。
    /// Binance futures service (read-only, may be null).
    /// </summary>
    protected IBinanceUsdFutureService? BinanceService { get; }

    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Signal>> GenerateSignalsAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        try
        {
            return await GenerateCoreAsync(context, ct).ConfigureAwait(false);
        }
        catch (PipelineAbortException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 策略层异常不得导致整个管道崩溃：记录错误并按"无信号"降级。
            // A strategy-level fault must not crash the pipeline: record it and degrade to "no signal".
            context.AddError(ex);
            context.AddEvent(PipelineEvent.Create(context.RunId, Id, $"generator fault (degraded to no signal): {ex.Message}"));
            return Array.Empty<Signal>();
        }
    }

    /// <summary>
    /// 由具体生成器实现的信号生成逻辑。
    /// Signal generation logic implemented by concrete generators.
    /// </summary>
    /// <param name="context">管道上下文 / Pipeline context.</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>信号列表（可能为空）/ Signals (possibly empty).</returns>
    protected abstract Task<IReadOnlyList<Signal>> GenerateCoreAsync(IPipelineContext context, CancellationToken ct);

    /// <summary>
    /// 读取标的收盘价序列（升序）；无数据时返回空序列并记录事件。
    /// Reads the close-price series (ascending) for a symbol; returns an empty series with an event when no data exists.
    /// </summary>
    /// <param name="context">管道上下文 / Pipeline context.</param>
    /// <param name="symbol">标的代码 / Trading symbol.</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>升序收盘价序列 / Ascending close prices.</returns>
    /// <exception cref="ArgumentException">symbol 为空白时抛出 / Thrown when symbol is blank.</exception>
    protected async Task<IReadOnlyList<double>> ResolveClosesAsync(IPipelineContext context, string symbol, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        if (SignalDataLoader.HasCachedSeries(context, symbol))
        {
            return await SignalDataLoader.LoadClosesAsync(context, symbol, ct, YahooData, BinanceService).ConfigureAwait(false);
        }

        var closes = await SignalDataLoader.LoadClosesAsync(context, symbol, ct, YahooData, BinanceService).ConfigureAwait(false);
        if (closes.Count == 0)
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, Id, $"insufficient data for '{symbol}'"));
        }

        return closes;
    }

    /// <summary>
    /// 计算总体标准差（population std dev）。
    /// Computes the population standard deviation.
    /// </summary>
    /// <param name="values">数值序列 / Values.</param>
    /// <returns>标准差 / Standard deviation.</returns>
    protected static double PopulationStdDev(IReadOnlyList<double> values)
        => OrchestrationNumerics.PopulationStdDev(values);

    /// <summary>
    /// 读取整型参数（InvariantCulture）。
    /// Reads an integer parameter (InvariantCulture).
    /// </summary>
    /// <param name="context">上下文 / Context.</param>
    /// <param name="key">参数名 / Parameter name.</param>
    /// <param name="defaultValue">缺省值 / Default value.</param>
    /// <returns>参数值 / Parsed value.</returns>
    protected static int GetIntParam(IPipelineContext context, string key, int defaultValue)
        => SignalParams.GetInt(context, key, defaultValue);

    /// <summary>
    /// 读取双精度参数（InvariantCulture）。
    /// Reads a double parameter (InvariantCulture).
    /// </summary>
    /// <param name="context">上下文 / Context.</param>
    /// <param name="key">参数名 / Parameter name.</param>
    /// <param name="defaultValue">缺省值 / Default value.</param>
    /// <returns>参数值 / Parsed value.</returns>
    protected static double GetDoubleParam(IPipelineContext context, string key, double defaultValue)
        => SignalParams.GetDouble(context, key, defaultValue);

    /// <summary>
    /// 读取布尔参数（InvariantCulture）。
    /// Reads a boolean parameter (InvariantCulture).
    /// </summary>
    /// <param name="context">上下文 / Context.</param>
    /// <param name="key">参数名 / Parameter name.</param>
    /// <param name="defaultValue">缺省值 / Default value.</param>
    /// <returns>参数值 / Parsed value.</returns>
    protected static bool GetBoolParam(IPipelineContext context, string key, bool defaultValue)
        => SignalParams.GetBool(context, key, defaultValue);
}


