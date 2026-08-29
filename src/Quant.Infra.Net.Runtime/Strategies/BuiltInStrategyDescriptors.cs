using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Runtime.Strategies;

/// <summary>
/// 内置 MaCross（均线交叉）策略描述符（U4：内置 3 个策略共用本文件登记）。
/// Built-in MaCross (moving-average cross) strategy descriptor (U4: the 3 built-ins are registered together in this file).
/// </summary>
public sealed class MaCrossDescriptor : IStrategyDescriptor
{
    /// <summary>策略名（与 Orchestration.Parameters.Strategy 的合法取值一致）/ Strategy name (one of the legal Orchestration.Parameters.Strategy values).</summary>
    public string Name => MaCrossSignalGenerator.GeneratorId;

    /// <summary>创建 MaCross 信号生成器（依赖解析与编排层 DI 工厂一致）/ Creates the MaCross generator with the same dependency wiring as the orchestration DI factory.</summary>
    /// <param name="serviceProvider">统一容器 / Unified service provider.</param>
    public ISignalGenerator Create(IServiceProvider serviceProvider)
        => new MaCrossSignalGenerator(
            serviceProvider.GetRequiredService<IAnalysisService>(),
            serviceProvider.GetService<ITraditionalFinanceSourceDataService>(),
            serviceProvider.GetRequiredService<IBinanceUsdFutureService>());
}

/// <summary>
/// 内置 MeanReversion（均值回归）策略描述符（U4）。
/// Built-in MeanReversion strategy descriptor (U4).
/// </summary>
public sealed class MeanReversionDescriptor : IStrategyDescriptor
{
    /// <summary>策略名 / Strategy name.</summary>
    public string Name => MeanReversionSignalGenerator.GeneratorId;

    /// <summary>创建 MeanReversion 信号生成器 / Creates the MeanReversion generator.</summary>
    /// <param name="serviceProvider">统一容器 / Unified service provider.</param>
    public ISignalGenerator Create(IServiceProvider serviceProvider)
        => new MeanReversionSignalGenerator(
            serviceProvider.GetRequiredService<IAnalysisService>(),
            serviceProvider.GetService<ITraditionalFinanceSourceDataService>(),
            serviceProvider.GetRequiredService<IBinanceUsdFutureService>());
}

/// <summary>
/// 内置 PairTradingZScore（配对交易 Z 分数）策略描述符（U4）。
/// Built-in PairTradingZScore strategy descriptor (U4).
/// </summary>
public sealed class PairTradingZScoreDescriptor : IStrategyDescriptor
{
    /// <summary>策略名 / Strategy name.</summary>
    public string Name => PairTradingZScoreSignalGenerator.GeneratorId;

    /// <summary>创建 PairTradingZScore 信号生成器 / Creates the PairTradingZScore generator.</summary>
    /// <param name="serviceProvider">统一容器 / Unified service provider.</param>
    public ISignalGenerator Create(IServiceProvider serviceProvider)
        => new PairTradingZScoreSignalGenerator(
            serviceProvider.GetRequiredService<IAnalysisService>(),
            serviceProvider.GetService<ITraditionalFinanceSourceDataService>(),
            serviceProvider.GetRequiredService<IBinanceUsdFutureService>());
}
