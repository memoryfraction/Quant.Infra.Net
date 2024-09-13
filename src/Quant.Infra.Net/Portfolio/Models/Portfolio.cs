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
    public abstract class PortfolioBase
    {
        public decimal InitCapital { get; set; } = 10000m;
        public abstract Currency BaseCurrency { get; set; }
        public List<OrderBase> Orders { get; set; } = new List<OrderBase>();
        public Dictionary<DateTime, PortfolioSnapshot> PortfolioSnapshots = new Dictionary<DateTime, PortfolioSnapshot>();

        // 用于存储每个时间点的市场价值
        public Dictionary<DateTime, decimal> MarketValueDic = new Dictionary<DateTime, decimal>();

        protected PortfolioBase()
        {
            Orders = new List<OrderBase>();
            PortfolioSnapshots = new Dictionary<DateTime, PortfolioSnapshot>();
            MarketValueDic = new Dictionary<DateTime, decimal>();
        }

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

            // 计算 MarketValue 并更新 marketValueDic
            decimal marketValue = balance.MarketValue;
            MarketValueDic[dateTime] = marketValue;
        }


        /// <summary>
        /// 更新持仓标的物的价格序列，并更新 MarketValueDic。
        /// </summary>
        /// <param name="symbolDictionary"></param>
        public void UpdateMarketValues(Dictionary<string, List<Ohlcv>> symbolOhlcvDic, ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        {
            if (symbolOhlcvDic == null || !symbolOhlcvDic.Any())
                return;

            // Determine the earliest and latest date in the portfolio snapshots and symbol data
            var earliestSnapshotDate = PortfolioSnapshots.Keys.Min();
            var latestSymbolDate = symbolOhlcvDic.Values.SelectMany(ohlcvs => ohlcvs.Select(x => x.OpenDateTime)).Max();

            // Define the start and end dates based on the available data
            var startDt = earliestSnapshotDate;
            var endDt = latestSymbolDate;

            // 根据resolutionLevel调整遍历Interval, 更新MarketValues
            // Get the interval based on the resolution level
            TimeSpan interval = UtilityService.GetInterval(resolutionLevel);

            // Create a list of date/time points to update
            var datePoints = new List<DateTime>();
            for (var dt = startDt; dt <= endDt; dt += interval)
            {
                datePoints.Add(dt);
            }

            // Update MarketValueDic for each date point
            foreach (var datePoint in datePoints)
            {
                if (PortfolioSnapshots.TryGetValue(datePoint, out var snapshot))
                {
                    decimal marketValue = 0;

                    // Calculate the market value based on current positions and available symbol data
                    foreach (var position in snapshot.Positions.PositionList)
                    {
                        if (symbolOhlcvDic.TryGetValue(position.Symbol, out var ohlcvList))
                        {
                            var ohlcv = FindNearestOhlcv(ohlcvList, datePoint.Date);
                            if (ohlcv != null)
                            {
                                marketValue += position.Quantity * ohlcv.Close;
                            }
                        }
                    }

                    // Update MarketValueDic
                    MarketValueDic[datePoint] = marketValue;
                }
            }

        }

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
        /// 根据MarketValueDic绘制Chart，横轴是日期，纵轴是decimal类型的Market Value。 并显示
        /// </summary>
        public async Task DrawChart(bool showChart = false)
        {
            // Convert MarketValueDic to lists of dates and market values
            var dateList = MarketValueDic.Keys.ToList();
            var marketValueList = MarketValueDic.Values.Select(x => (double)x).ToList();

            // Create a new Plot object
            var plt = new Plot();

            // Plot market values against dates
            plt.Add.Scatter(dateList, marketValueList);

            // Format the X-axis as date
            plt.Axes.DateTimeTicksBottom();

            // Set the chart title and axis labels
            plt.Title("Portfolio Market Value Over Time");
            plt.XLabel("Date");
            plt.YLabel("Market Value");

            // Optionally save the plot as an image
            string fullPathFilename = Path.Combine(AppContext.BaseDirectory, "output", "PortfolioMarketValue.png");
            await UtilityService.IsPathExistAsync(fullPathFilename);
            plt.SaveJpeg(fullPathFilename, 600, 400);

            // Optionally display the chart if showChart is true
            if (showChart)
            {
                // Open the generated image using the default program
                Process.Start(new ProcessStartInfo(fullPathFilename) { UseShellExecute = true });
            }
        }
    }



    // 股票投资组合
    public class StockPortfolio : PortfolioBase
    {
        public override Currency BaseCurrency { get; set; } = Currency.USD;


    }


    // 加密货币现货投资组合
    public class CryptoSpotPortfolio : PortfolioBase
    {
        public override Currency BaseCurrency { get; set; } = Currency.USDT;


    }


    // 永续合约投资组合
    public class CryptoPerpetualContractPortfolio : PortfolioBase
    {
        public override Currency BaseCurrency { get; set; } = Currency.USDT;

    }


    public class PortfolioSnapshot
    {
        public DateTime DateTime { get; set; }
        public Balance Balance { get; set; } = new Balance();
        public Positions Positions { get; set; } = new Positions();
    }
}