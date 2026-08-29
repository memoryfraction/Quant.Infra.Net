using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Backtest.Tests;

/// <summary>
/// 测试用 K 线工厂 / candle factory for tests.
/// </summary>
internal static class TestBars
{
    /// <summary>
    /// 构造一根全价位相等的 K 线（OHLC 均取 price）。
    /// Builds a single candle with O=H=L=C=price.
    /// </summary>
    /// <param name="symbol">标的 / symbol.</param>
    /// <param name="t">K 线开盘时间 / candle open time.</param>
    /// <param name="price">价格 / price.</param>
    /// <returns>K 线 / the candle.</returns>
    public static Ohlcv Bar(string symbol, DateTime t, decimal price) => new()
    {
        Symbol = symbol,
        OpenDateTime = t,
        CloseDateTime = t,
        Open = price,
        High = price,
        Low = price,
        Close = price,
        Volume = 1m,
    };
}
