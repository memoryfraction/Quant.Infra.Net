namespace Quant.Infra.Net.Runtime.Models;

/// <summary>运行模式：决定驱动循环与经纪商实现，策略代码无需感知 / Run mode: decides the driver loop and broker implementation; strategy code is unaware of it.</summary>
public enum RunMode
{
    /// <summary>历史回放：BacktestRunner 驱动，BacktestBrokerService 记账，零网络 / Historical replay: driven by BacktestRunner, accounted by BacktestBrokerService, zero network.</summary>
    Backtest = 0,

    /// <summary>纸上交易：PipelineRunner+IntervalTrigger 墙钟驱动，PaperBinanceUsdFutureService 记账，零网络 / Paper trading: wall-clock driven by PipelineRunner+IntervalTrigger, accounted by PaperBinanceUsdFutureService, zero network.</summary>
    Paper = 1,

    /// <summary>测试网实盘：真实 Binance Testnet API / Binance testnet: real Testnet API calls.</summary>
    Testnet = 2,

    /// <summary>生产实盘：真实资金，真实 Binance Live API / Production live: real funds, real Binance Live API calls.</summary>
    Live = 3
}
