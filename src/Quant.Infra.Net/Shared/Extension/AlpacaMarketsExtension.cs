using Quant.Infra.Net;
using Quant.Infra.Net.Shared.Model;
using System;

namespace Quant.Infra.Net.Shared.Extension
{
    /// <summary>
    /// 枚举转换工具类
    /// </summary>
    public static class AlpacaMarketsExtension
    {
        /// <summary>
        /// 将 Quant.Infra.Net.TimeInForce 转换为 Alpaca.Markets.TimeInForce
        /// </summary>
        /// <param name="timeInForce">源枚举值</param>
        /// <returns>对应的 Alpaca TimeInForce 枚举值</returns>
        /// <exception cref="ArgumentOutOfRangeException">当传入枚举无对应转换时抛出</exception>
        public static Alpaca.Markets.TimeInForce ToAlpacaTimeInForce(TimeInForce timeInForce)
        {
            return timeInForce switch
            {
                TimeInForce.GoodTillCanceled => Alpaca.Markets.TimeInForce.Gtc,        // Gtc 对应 GoodTillCanceled
                TimeInForce.ImmediateOrCancel => Alpaca.Markets.TimeInForce.Ioc,      // Ioc 对应 ImmediateOrCancel
                TimeInForce.FillOrKill => Alpaca.Markets.TimeInForce.Fok,             // Fok 对应 FillOrKill
                TimeInForce.GoodTillDate => Alpaca.Markets.TimeInForce.Day,           // Day 用作 GoodTillDate 映射（无更合适枚举）
                TimeInForce.GoodTillCrossing => throw new NotSupportedException("GoodTillCrossing is not supported by Alpaca API."),
                TimeInForce.GoodTillExpiredOrCanceled => throw new NotSupportedException("GoodTillExpiredOrCanceled is not supported by Alpaca API."),
                _ => throw new ArgumentOutOfRangeException(nameof(timeInForce), timeInForce, "Unsupported TimeInForce value")
            };
        }


        /// <summary>
        /// 将 Quant.Infra.Net.OrderExecutionType 枚举值转换为 Alpaca.Markets.OrderType。
        /// </summary>
        /// <param name="executionType">自定义 OrderExecutionType 枚举值。</param>
        /// <returns>对应的 Alpaca OrderType 枚举值。</returns>
        /// <exception cref="ArgumentOutOfRangeException">当无法映射时抛出异常。</exception>
        public static Alpaca.Markets.OrderType ToAlpacaOrderType(OrderExecutionType executionType)
        {
            return executionType switch
            {
                OrderExecutionType.Market => Alpaca.Markets.OrderType.Market,
                OrderExecutionType.Limit => Alpaca.Markets.OrderType.Limit,
                OrderExecutionType.StopLoss => Alpaca.Markets.OrderType.Stop,
                OrderExecutionType.StopLossLimit => Alpaca.Markets.OrderType.StopLimit,
                OrderExecutionType.TrailingStop => Alpaca.Markets.OrderType.TrailingStop,


                // Not directly supported by Alpaca's OrderType enum
                OrderExecutionType.TakeProfit => throw new NotSupportedException("TakeProfit is not directly supported. Consider using a Limit order with conditional logic."),
                OrderExecutionType.TakeProfitLimit => throw new NotSupportedException("TakeProfitLimit is not directly supported. Consider using a Limit order with conditional logic."),
                OrderExecutionType.FillOrKill => throw new NotSupportedException("FillOrKill should be set using TimeInForce, not OrderType."),
                OrderExecutionType.ImmediateOrCancel => throw new NotSupportedException("ImmediateOrCancel should be set using TimeInForce, not OrderType."),

                _ => throw new ArgumentOutOfRangeException(nameof(executionType), executionType, "Unsupported OrderExecutionType value.")
            };
        }
    }
}
