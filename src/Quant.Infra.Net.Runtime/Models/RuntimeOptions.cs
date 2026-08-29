namespace Quant.Infra.Net.Runtime.Models;

/// <summary>统一运行时配置（DI 绑定到 "Runtime" 配置节）/ Unified runtime configuration (bound to the "Runtime" section).</summary>
public class RuntimeOptions
{
    /// <summary>运行模式（决定驱动循环与经纪商；默认 Backtest 最安全）/ Run mode (driver + broker; defaults to the safest Backtest).</summary>
    public RunMode RunMode { get; set; } = RunMode.Backtest;

    /// <summary>数据源种类（默认 Demo 离线合成，零网络）/ Data source kind (defaults to the offline Demo kind).</summary>
    public DataSourceKind DataSource { get; set; } = DataSourceKind.Demo;

    /// <summary>Testnet/Live 模式下的 Binance API Key（Backtest/Paper 模式下忽略）/ Binance API key for Testnet/Live (ignored otherwise).</summary>
    public string? BinanceApiKey { get; set; }

    /// <summary>Testnet/Live 模式下的 Binance API Secret（Backtest/Paper 模式下忽略）/ Binance API secret for Testnet/Live (ignored otherwise).</summary>
    public string? BinanceApiSecret { get; set; }

    /// <summary>DataSource=Alpaca 时的 Alpaca API Key（其他数据源忽略；免费层在 alpaca.markets 申请）/ Alpaca API key when DataSource=Alpaca (ignored otherwise; free tier at alpaca.markets).</summary>
    public string? AlpacaApiKey { get; set; }

    /// <summary>DataSource=Alpaca 时的 Alpaca API Secret（其他数据源忽略）/ Alpaca API secret when DataSource=Alpaca (ignored otherwise).</summary>
    public string? AlpacaApiSecret { get; set; }
}
