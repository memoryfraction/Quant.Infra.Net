using Quant.Infra.Net.Portfolio.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.Portfolio.Services
{
    /// <summary>
    /// 该类包括静态方法，在给定输入的前提下，计算出balance和positions;
    /// 计算结果作为入参，提供给: portfolio.UpsertSnapshot(DateTime.UtcNow, balance, positions);
    /// </summary>
    public class PortfolioCalculationService
    {
        /// <summary>
        /// 计算 Balance，包括 NetLiquidationValue, MarketValue, Cash 和 UnrealizedPnL
        /// </summary>
        /// <param name="portfolio">当前投资组合</param>
        /// <param name="cash">当前现金</param>
        /// <param name="latestPrices">最新价格列表，使用 Symbol 作为键，最新价格作为值</param>
        /// <param name="dateTimeUtc">当前的 UTC 时间</param>
        /// <returns>更新后的 Balance 对象</returns>
        public static Balance CalculateBalance(PortfolioBase portfolio, decimal cash, Dictionary<string, decimal> latestPrices, DateTime dateTimeUtc)
        {
            decimal marketValue = 0; // 持仓的市场价值，不包括cash
            decimal unrealizedPnL = 0;

            // 遍历持仓，计算市场价值和未实现盈亏
            foreach (var position in portfolio.PortfolioSnapshots.Values.LastOrDefault()?.Positions?.PositionList ?? new List<Position>())
            {
                if (latestPrices.TryGetValue(position.Symbol, out decimal latestPrice))
                {
                    marketValue += position.GetMarketValue(latestPrice);
                    unrealizedPnL += position.GetUnrealizedPnL(latestPrice);
                }
            }

            // 计算净清算价值
            decimal netLiquidationValue = cash + marketValue;

            return new Balance
            {
                DateTime = dateTimeUtc,
                NetLiquidationValue = netLiquidationValue,
                MarketValue = marketValue,
                Cash = cash,
                UnrealizedPnL = unrealizedPnL
            };
        }

        /// <summary>
        /// 计算 Positions，通过已成交订单和当前持仓更新持仓状态
        /// </summary>
        /// <param name="portfolio">当前投资组合</param>
        /// <param name="filledOrder">已成交订单</param>
        /// <returns>更新后的 Positions 对象</returns>
        public static Positions CalculatePositions(PortfolioBase portfolio, OrderBase filledOrder)
        {
            var currentPositions = portfolio.PortfolioSnapshots.Values.LastOrDefault()?.Positions?.PositionList ?? new List<Position>();

            var existingPosition = currentPositions.FirstOrDefault(p => p.Symbol == filledOrder.Symbol);

            decimal orderQuantity = filledOrder.Quantity ?? 0;  // Handle null Quantity
            decimal orderPrice = filledOrder.Price ?? 0;        // Handle null Price

            if (existingPosition != null)
            {
                // 如果订单是买单，增加持仓
                if (orderQuantity > 0)
                {
                    decimal newQuantity = existingPosition.Quantity + orderQuantity;
                    existingPosition.CostPrice = (existingPosition.CostPrice * existingPosition.Quantity + orderPrice * orderQuantity) / newQuantity;
                    existingPosition.Quantity = newQuantity;
                }
                else // 如果订单是卖单，减少持仓
                {
                    existingPosition.Quantity += orderQuantity; // filledOrder.Quantity 应该是负值
                }
            }
            else
            {
                // 如果当前没有该股票的持仓，则创建新持仓
                currentPositions.Add(new Position
                {
                    EntryDateTime = filledOrder.DateTimeUtc,  // 使用订单的 UTC 时间
                    Symbol = filledOrder.Symbol,
                    Quantity = orderQuantity,
                    CostPrice = orderPrice
                });
            }

            // 清理零持仓
            currentPositions = currentPositions.Where(p => p.Quantity != 0).ToList();

            return new Positions
            {
                DateTime = filledOrder.DateTimeUtc,  // 使用订单的 UTC 时间
                PositionList = currentPositions
            };
        }
    }
}