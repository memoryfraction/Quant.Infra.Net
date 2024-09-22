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

        /// <summary>
        /// Constructor of PortfolioBase.
        /// PortfolioBase的构造函数
        /// </summary>
        protected PortfolioBase()
        {
            Orders = new List<OrderBase>();
            PortfolioSnapshots = new Dictionary<DateTime, PortfolioSnapshot>();
            MarketValueDic = new Dictionary<DateTime, decimal>();
        }

        /// <summary>
        /// Insert or update a snapshot for a specific time.
        /// 插入或更新特定时间的快照
        /// </summary>
        /// <param name="dateTime">Time of the snapshot. 快照时间</param>
        /// <param name="balance">Portfolio balance. 投资组合余额</param>
        /// <param name="positions">Portfolio positions. 投资组合持仓</param>
        public void UpsertSnapshot(DateTime dateTime, Balance balance, Positions positions)
        {
            if (PortfolioSnapshots.ContainsKey(dateTime))
            {
                PortfolioSnapshots[dateTime].Balance = balance;
                PortfolioSnapshots[dateTime].Positions = positions;
            }
            else
            {
                PortfolioSnapshots.Add(dateTime, new PortfolioSnapshot
                {
                    DateTime = dateTime,
                    Balance = balance,
                    Positions = positions
                });
            }

            // 更新MarketValueDic
            decimal marketValue = balance.MarketValue;
            MarketValueDic[dateTime] = marketValue;
        }

        /// <summary>
        /// Update the price series of the assets in the portfolio, and update MarketValueDic, considering different AssetTypes.
        /// 更新投资组合中资产的价格序列，并更新MarketValueDic，同时考虑不同的资产类型。
        /// </summary>
        /// <param name="symbolOhlcvDic">Dictionary containing the OHLCV data for each symbol. 每个符号的OHLCV数据字典</param>
        /// <param name="resolutionLevel">Resolution level for the time series. 时间序列的分辨率</param>
        public void UpdateMarketValues(Dictionary<string, List<Ohlcv>> symbolOhlcvDic, ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        {
            if (symbolOhlcvDic == null || !symbolOhlcvDic.Any())
                return;

            var earliestSnapshotDate = PortfolioSnapshots.Keys.Min();
            var latestSymbolDate = symbolOhlcvDic.Values.SelectMany(ohlcvs => ohlcvs.Select(x => x.OpenDateTime)).Max();

            var startDt = earliestSnapshotDate;
            var endDt = latestSymbolDate;
            TimeSpan interval = UtilityService.GetInterval(resolutionLevel);

            var datePoints = new List<DateTime>();
            for (var dt = startDt; dt <= endDt; dt += interval)
            {
                datePoints.Add(dt);
            }

            foreach (var datePoint in datePoints)
            {
                if (PortfolioSnapshots.TryGetValue(datePoint, out var snapshot))
                {
                    decimal marketValue = 0;

                    foreach (var position in snapshot.Positions.PositionList)
                    {
                        if (symbolOhlcvDic.TryGetValue(position.Symbol, out var ohlcvList))
                        {
                            var ohlcv = FindNearestOhlcv(ohlcvList, datePoint.Date);
                            if (ohlcv != null)
                            {
                                // 根据资产类型进行市场价值计算
                                switch (position.AssetType)
                                {
                                    case AssetType.CryptoSpot:
                                        marketValue += position.Quantity * ohlcv.Close;
                                        break;

                                    case AssetType.CryptoPerpetualContract:
                                        // 对期货仓位的特殊处理，例如杠杆计算
                                        marketValue += position.Quantity * ohlcv.Close; // 示例，具体逻辑取决于你的业务规则
                                        break;
                                }
                            }
                        }
                    }

                    MarketValueDic[datePoint] = marketValue;
                }
            }
        }

        /// <summary>
        /// Find the OHLCV data closest to the given date.
        /// 查找最接近给定日期的OHLCV数据
        /// </summary>
        /// <param name="ohlcvList">List of OHLCV data. OHLCV数据列表</param>
        /// <param name="datePoint">Date to find the nearest OHLCV data for. 要查找最近OHLCV数据的日期</param>
        /// <returns>Nearest OHLCV data. 最近的OHLCV数据</returns>
        private Ohlcv FindNearestOhlcv(List<Ohlcv> ohlcvList, DateTime datePoint)
        {
            Ohlcv nearestOhlcv = null;
            TimeSpan smallestTimeDifference = TimeSpan.MaxValue;

            foreach (var ohlcv in ohlcvList)
            {
                var timeDifference = Math.Abs((ohlcv.OpenDateTime.Date - datePoint.Date).Ticks);

                if (timeDifference < smallestTimeDifference.Ticks)
                {
                    smallestTimeDifference = TimeSpan.FromTicks(timeDifference);
                    nearestOhlcv = ohlcv;
                }
            }

            return nearestOhlcv;
        }

        /// <summary>
        /// Plot the market value of the portfolio over time, supporting different asset types.
        /// 绘制投资组合的市值变化图，支持不同的资产类型。
        /// </summary>
        /// <param name="showChart">Whether to display the chart. 是否显示图表</param>
        public async Task DrawChart(bool showChart = false)
        {
            var dateList = MarketValueDic.Keys.ToList();
            var marketValueList = MarketValueDic.Values.Select(x => (double)x).ToList();

            var plt = new Plot();
            plt.Add.Scatter(dateList, marketValueList);
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
        public override Currency BaseCurrency { get; set; } = Currency.USD;
    }

    /// <summary>
    /// Crypto perpetual contract portfolio implementation.
    /// 永续合约投资组合实现
    /// </summary>
    public class CryptoPortfolio : PortfolioBase
    {
        public override Currency BaseCurrency { get; set; } = Currency.USDT;
    }

    /// <summary>
    /// Snapshot of the portfolio at a specific time.
    /// 特定时间的投资组合快照
    /// </summary>
    public class PortfolioSnapshot
    {
        public DateTime DateTime { get; set; }
        public Balance Balance { get; set; } = new Balance();
        public Positions Positions { get; set; } = new Positions();
    }
}
