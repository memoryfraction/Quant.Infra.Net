using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Model;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Quant.Infra.Net.Account.Service;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.SourceData.Service.RealTime;

namespace Quant.Infra.Net.Portfolio.Models
{
    /// <summary>
    /// Base class for portfolio management.
    /// 投资组合管理的基类
    /// </summary>
    public abstract class PortfolioBase
    {
        /// <summary>
        /// Initial capital of the portfolio.
        /// 投资组合的初始资本
        /// </summary>
        public decimal InitCapital { get; set; } = 10000m;

        /// <summary>
        /// Base currency of the portfolio.
        /// 投资组合的基础货币
        /// </summary>
        public abstract Currency BaseCurrency { get; set; }

        /// <summary>
        /// List of orders in the portfolio.
        /// 投资组合中的订单列表
        /// </summary>
        public List<OrderBase> Orders { get; set; } = new List<OrderBase>();

        /// <summary>
        /// Portfolio snapshots at different time points.
        /// 不同时点的投资组合快照
        /// </summary>
        public Dictionary<DateTime, PortfolioSnapshot> PortfolioSnapshots { get; set; } = new Dictionary<DateTime, PortfolioSnapshot>();

        /// <summary>
        /// Dictionary to store the market value at each time point.
        /// 用于存储每个时间点的市场价值
        /// </summary>
        public Dictionary<DateTime, decimal> MarketValueDic { get; set; } = new Dictionary<DateTime, decimal>();

        protected PortfolioBase()
        {
            Orders = new List<OrderBase>();
            PortfolioSnapshots = new Dictionary<DateTime, PortfolioSnapshot>();
            MarketValueDic = new Dictionary<DateTime, decimal>();
        }

        /// <summary>
        /// Gets the total unlevered absolute holdings cost.
        /// 获取总未杠杆绝对持仓成本
        /// </summary>
        public decimal TotalUnleveredAbsoluteHoldingsCost =>
            PortfolioSnapshots.Values.Sum(snapshot =>
                snapshot.Positions.PositionList.Sum(position => position.Quantity * position.CostPrice));

        /// <summary>
        /// Calculates total unrealized profit based on the latest prices.
        /// 计算基于最新价格的总未实现利润
        /// </summary>
        public abstract Task<decimal> TotalUnrealisedProfitAsync();

        /// <summary>
        /// Calculates total holdings value based on the latest prices.
        /// 计算基于最新价格的总持仓价值
        /// </summary>
        public abstract Task<decimal> HoldingsValueAsync(Underlying underlying);



        /// <summary>
        /// Gets the quantity of a specific underlying asset.
        /// 获取特定基础资产的数量
        /// </summary>
        public decimal GetUnderlyingQuantity(Underlying underlying) =>
            PortfolioSnapshots.Values
                .SelectMany(snapshot => snapshot.Positions.PositionList)
                .Where(position => position.Symbol.ToLower() == underlying.Symbol.ToLower() && position.AssetType == underlying.AssetType)
                .Sum(position => position.Quantity);

        /// <summary>
        /// Inserts or updates a snapshot for a specific time.
        /// 插入或更新特定时间的快照
        /// </summary>
        public void UpsertSnapshot(DateTime dateTime, Balance balance, Positions positions)
        {
            if (PortfolioSnapshots.ContainsKey(dateTime))
            {
                PortfolioSnapshots[dateTime].Balance = balance;
                PortfolioSnapshots[dateTime].Positions = positions;
            }
            else
            {
                PortfolioSnapshots[dateTime] = new PortfolioSnapshot
                {
                    DateTime = dateTime,
                    Balance = balance,
                    Positions = positions
                };
            }

            MarketValueDic[dateTime] = balance.MarketValue;
        }

        /// <summary>
        /// Updates the price series of the assets in the portfolio and adjusts market values.
        /// 更新投资组合中资产的价格序列并调整市场价值
        /// </summary>
        public void UpdateMarketValues(Dictionary<string, List<Ohlcv>> symbolOhlcvDic, ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        {
            if (symbolOhlcvDic == null || !symbolOhlcvDic.Any())
                return;

            var earliestSnapshotDate = PortfolioSnapshots.Keys.Min();
            var latestSymbolDate = symbolOhlcvDic.Values.SelectMany(ohlcvs => ohlcvs.Select(x => x.OpenDateTime)).Max();

            var datePoints = Enumerable.Range(0, (int)(latestSymbolDate - earliestSnapshotDate).TotalDays + 1)
                .Select(offset => earliestSnapshotDate.AddDays(offset))
                .ToList();

            foreach (var datePoint in datePoints)
            {
                if (PortfolioSnapshots.TryGetValue(datePoint, out var snapshot))
                {
                    decimal marketValue = snapshot.Positions.PositionList.Sum(position =>
                    {
                        if (symbolOhlcvDic.TryGetValue(position.Symbol, out var ohlcvList))
                        {
                            var ohlcv = FindNearestOhlcv(ohlcvList, datePoint.Date);
                            if (ohlcv != null)
                            {
                                return position.AssetType switch
                                {
                                    AssetType.CryptoSpot => position.Quantity * ohlcv.Close,
                                    AssetType.CryptoPerpetualContract => position.Quantity * ohlcv.Close, // Adjust for leverage if necessary
                                    _ => 0
                                };
                            }
                        }
                        return 0;
                    });

                    MarketValueDic[datePoint] = marketValue;
                }
            }
        }

        /// <summary>
        /// Finds the OHLCV data closest to the given date.
        /// 查找最接近给定日期的OHLCV数据
        /// </summary>
        private Ohlcv FindNearestOhlcv(List<Ohlcv> ohlcvList, DateTime datePoint)
        {
            return ohlcvList.OrderBy(ohlcv => Math.Abs((ohlcv.OpenDateTime.Date - datePoint.Date).Ticks)).FirstOrDefault();
        }

        /// <summary>
        /// Plots the market value of the portfolio over time.
        /// 绘制投资组合的市值变化图
        /// </summary>
        public async Task DrawChart(bool showChart = false)
        {
            var plt = new Plot();
            plt.Add.Scatter(MarketValueDic.Keys.Select(dt => dt.ToOADate()).ToArray(), MarketValueDic.Values.Select(x => (double)x).ToArray());
            plt.Axes.DateTimeTicksBottom();
            plt.Title("Portfolio Market Value Over Time");
            plt.XLabel("Date");
            plt.YLabel("Market Value");

            string fullPathFilename = Path.Combine(AppContext.BaseDirectory, "output", "PortfolioMarketValue.png");
            await UtilityService.IsPathExistAsync(fullPathFilename);
            plt.SaveJpeg(fullPathFilename, 600, 400);

            if (showChart)
            {
                Process.Start(new ProcessStartInfo(fullPathFilename) { UseShellExecute = true });
            }
        }
    }

    /// <summary>
    /// Stock portfolio implementation.
    /// 股票投资组合实现
    /// </summary>
    public class StockPortfolio : PortfolioBase
    {
        private readonly IRealtimeDataSourceService _realtimeDataSourceService;

        public StockPortfolio(IRealtimeDataSourceService realtimeDataSourceService)
        {
            _realtimeDataSourceService = realtimeDataSourceService;
        }

        /// <summary>
        /// Base currency for the stock portfolio.
        /// 股票投资组合的基础货币
        /// </summary>
        public override Currency BaseCurrency { get; set; } = Currency.USD;

        public override async Task<decimal> HoldingsValueAsync(Underlying underlying)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calculates total unrealized profit for the stock portfolio.
        /// 计算股票投资组合的总未实现利润
        /// </summary>
        public async override Task<decimal> TotalUnrealisedProfitAsync()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Crypto perpetual contract portfolio implementation.
    /// 永续合约投资组合实现
    /// </summary>
    public class CryptoPortfolio : PortfolioBase
    {
        private readonly IRealtimeDataSourceService _realtimeDataSourceService;

        public CryptoPortfolio(IRealtimeDataSourceService realtimeDataSourceService)
        {
            _realtimeDataSourceService = realtimeDataSourceService;
        }

        /// <summary>
        /// Base currency for the crypto portfolio.
        /// 加密货币投资组合的基础货币
        /// </summary>
        public override Currency BaseCurrency { get; set; } = Currency.USDT;


        /// <summary>
        /// Calculates the total holdings value for a specific underlying asset in the crypto portfolio.
        /// 计算加密货币投资组合中特定基础资产的总持仓价值
        /// </summary>
        /// <param name="underlying">The underlying asset for which to calculate the holdings value.</param>
        /// <returns>A task representing the asynchronous operation, with the total holdings value as its result.</returns>
        public override async Task<decimal> HoldingsValueAsync(Underlying underlying)
        {
            // Get the latest price for the underlying asset
            decimal latestPrice = await _realtimeDataSourceService.GetLatestPriceAsync(underlying);

            // Calculate total holdings value for the specified underlying asset
            decimal totalHoldingsValue = PortfolioSnapshots.Values.Sum(snapshot =>
                snapshot.Positions.PositionList
                    .Where(position => position.Symbol.Equals(underlying.Symbol, StringComparison.OrdinalIgnoreCase))
                    .Sum(position => position.Quantity * latestPrice));

            return totalHoldingsValue;
        }


        /// <summary>
        /// Calculates total unrealized profit for the crypto portfolio.
        /// 计算加密货币投资组合的总未实现利润
        /// </summary>
        public override async Task<decimal> TotalUnrealisedProfitAsync()
        {
            // Get the latest prices for the assets in the portfolio
            var latestPrices = new Dictionary<string, decimal>();

            foreach (var position in PortfolioSnapshots.Values.SelectMany(snapshot => snapshot.Positions.PositionList))
            {
                if (!latestPrices.ContainsKey(position.Symbol))
                {
                    var underlying = new Underlying()
                    {
                        Symbol = position.Symbol,
                        AssetType = position.AssetType
                    };
                    var latestPrice = await _realtimeDataSourceService.GetLatestPriceAsync(underlying);
                    latestPrices[position.Symbol] = latestPrice;
                }
            }

            // Calculate unrealized profit
            decimal totalUnrealizedProfit = PortfolioSnapshots.Values.Sum(snapshot =>
                snapshot.Positions.PositionList.Sum(position =>
                {
                    if (latestPrices.TryGetValue(position.Symbol, out var latestPrice))
                    {
                        return (latestPrice - position.CostPrice) * position.Quantity;
                    }
                    return 0;
                }));

            return totalUnrealizedProfit;
        }
    }

    /// <summary>
    /// Portfolio snapshot model.
    /// 投资组合快照模型
    /// </summary>
    public class PortfolioSnapshot
    {
        /// <summary>
        /// Date and time of the snapshot.
        /// 快照的日期和时间
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Balance details of the snapshot.
        /// 快照的余额详情
        /// </summary>
        public Balance Balance { get; set; }

        /// <summary>
        /// Positions details of the snapshot.
        /// 快照的持仓详情
        /// </summary>
        public Positions Positions { get; set; }
    }
}
