﻿using Quant.Infra.Net.Shared.Model;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Interfaces
{
    /// <summary>
    /// Interface for interacting with Binance USD-M Futures services.
    /// 与币安USD-M期货服务交互的接口。
    /// </summary>
    public interface IBinanceUsdFutureService
    {
        /// <summary>
        /// Retrieves the USD-based futures account balance.
        /// 获取以USD计价的期货账户余额。
        /// </summary>
        /// <returns>The total USD-based balance. 返回以USD计价的总余额。</returns>
        Task<decimal> GetusdFutureAccountBalanceAsync();

        /// <summary>
        /// Retrieves the unrealized profit rate of the portfolio.
        /// 获取投资组合的未变现利润率。
        /// </summary>
        /// <param name="LastOpenPortfolioMarketValue">The market value of the portfolio at the last open position. 上次开仓时的投资组合市值。</param>
        /// <returns>The unrealized profit rate. 返回未变现的利润率。</returns>
        Task<double> GetusdFutureUnrealizedProfitRateAsync(decimal LastOpenPortfolioMarketValue);

        /// <summary>
        /// Liquidates the open position for the specified symbol.
        /// 清算指定交易对的持仓。
        /// </summary>
        /// <param name="symbol">The trading symbol, e.g., "BTCUSDT". 交易对符号，例如"BTCUSDT"。</param>
        /// <returns>A task representing the asynchronous operation. 表示异步操作的任务。</returns>
        Task usdFutureLiquidateAsync(string symbol);

        /// <summary>
        /// Adjusts the futures position based on the specified portfolio rate.
        /// 根据指定的投资组合比例调整期货持仓。
        /// </summary>
        /// <param name="symbol">The trading symbol, e.g., "BTCUSDT". 交易对符号，例如"BTCUSDT"。</param>
        /// <param name="rate">The target portfolio rate. 目标投资组合比例。</param>
        /// <returns>A task representing the asynchronous operation. 表示异步操作的任务。</returns>
        Task SetUsdFutureHoldingsAsync(string symbol, double rate);

        /// <summary>
        /// Checks if there is an open position for the specified symbol.
        /// 检查指定交易对是否有未平仓头寸。
        /// </summary>
        /// <param name="symbol">The trading symbol, e.g., "BTCUSDT". 交易对符号，例如"BTCUSDT"。</param>
        /// <returns>True if there is an open position, false otherwise. 如果有未平仓头寸，则返回true；否则返回false。</returns>
        Task<bool> HasUsdFuturePositionAsync(string symbol);
    }
}
