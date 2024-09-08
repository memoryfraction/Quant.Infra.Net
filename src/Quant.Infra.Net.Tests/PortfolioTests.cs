using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Portfolio.Models;
using Quant.Infra.Net.Portfolio.Services;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class PortfolioTests
    {
        private ServiceCollection _services;
        private ServiceProvider _serviceProvider;
        private IConfigurationRoot _configuration;
        private PortfolioBase _portfolio;
        private Random _random;

        public PortfolioTests()
        {
            // 依赖注入
            _services = new ServiceCollection();
            _services.AddScoped<PortfolioBase, PerpetualContractPortfolio>();

            // 读取配置文件
            _configuration = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .Build();

            // 构建ServiceProvider
            _serviceProvider = _services.BuildServiceProvider();

            // 获取Portfolio实例
            _portfolio = _serviceProvider.GetService<PortfolioBase>();

            _random = new Random();
        }



        [TestMethod]
        public void UpsertSnapshot_Should_Work()
        {
            // Arrange
            var initialCash = 10000m; // Initial cash balance
            var fakePrices = new Dictionary<string, decimal>
            {
                { "AAPL", 150m },
                { "GOOGL", 2800m }
            };

            var currentDateTime = new DateTime(2023, 6, 15, 14, 30, 0, DateTimeKind.Utc); // Fixed historical date

            // Create a sample filled order with a fixed historical date
            var filledOrder = new OrderBase
            {
                Symbol = "AAPL",
                Quantity = 50,
                Price = 145m,
                DateTimeUtc = currentDateTime // Use the fixed historical date
            };

            // Simulate the portfolio snapshot before updating
            var initialPositions = new Positions
            {
                DateTime = new DateTime(2023, 6, 14, 14, 30, 0, DateTimeKind.Utc),
                PositionList = new List<Position>
                {
                    new Position
                    {
                        Symbol = "AAPL",
                        Quantity = 50,
                        CostPrice = 140m,
                        DateTime = new DateTime(2023, 6, 14, 14, 30, 0, DateTimeKind.Utc)
                    }
                }
            };

            // Simulate adding the initial snapshot to the portfolio
            _portfolio.PortfolioSnapshots.Add(new DateTime(2023, 6, 14, 14, 30, 0, DateTimeKind.Utc), new PortfolioSnapshot
            {
                DateTime = new DateTime(2023, 6, 14, 14, 30, 0, DateTimeKind.Utc),
                Positions = initialPositions,
                Balance = new Balance
                {
                    DateTime = new DateTime(2023, 6, 14, 14, 30, 0, DateTimeKind.Utc),
                    Cash = initialCash,
                    MarketValue = 0,
                    UnrealizedPnL = 0,
                    NetLiquidationValue = initialCash
                }
            });

            // Act
            // Calculate new positions and balance
            var updatedPositions = PortfolioCalculationService.CalculatePositions(_portfolio, filledOrder);
            var updatedBalance = PortfolioCalculationService.CalculateBalance(_portfolio, initialCash, fakePrices, currentDateTime);

            // Upsert the new snapshot with the fixed historical date
            _portfolio.UpsertSnapshot(currentDateTime, updatedBalance, updatedPositions);

            // Assert
            var lastSnapshot = _portfolio.PortfolioSnapshots.Values.LastOrDefault();
            Assert.IsNotNull(lastSnapshot, "Snapshot should be upserted.");
            Assert.AreEqual(updatedBalance.Cash, lastSnapshot.Balance.Cash, "Cash should match.");
            Assert.AreEqual(updatedBalance.NetLiquidationValue, lastSnapshot.Balance.NetLiquidationValue, "Net Liquidation Value should match.");
            Assert.AreEqual(updatedPositions.PositionList.Count, lastSnapshot.Positions.PositionList.Count, "Positions count should match.");
            Assert.AreEqual(updatedPositions.PositionList.First().Symbol, lastSnapshot.Positions.PositionList.First().Symbol, "Position symbol should match.");
            Assert.AreEqual(updatedPositions.PositionList.First().Quantity, lastSnapshot.Positions.PositionList.First().Quantity, "Position quantity should match.");
        }




        [TestMethod]
        public void CalculateCAGR_Should_Work()
        {
            // Arrange
            var marketValueDict = GenerateFakeMarketValueDict();

            // Extract values and dates
            var dates = marketValueDict.Keys.OrderBy(d => d).ToList();
            var values = marketValueDict.Values.ToList();

            if (dates.Count < 2) Assert.Fail("Not enough data to calculate CAGR.");

            var initialValue = values.First();
            var finalValue = values.Last();
            var startDate = dates.First();
            var endDate = dates.Last();
            var years = (endDate - startDate).TotalDays / 365.25;

            var expectedCAGR = (decimal)Math.Pow((double)(finalValue / initialValue), 1.0 / years) - 1;

            // Act
            var actualCAGR = StrategyPerformanceAnalyzer.CalculateCAGR(marketValueDict);

            // Assert
            Assert.AreEqual(expectedCAGR, actualCAGR, 0.0001m, "CAGR calculation is incorrect.");

            // Debugging output (optional)
            Console.WriteLine($"Initial Value: {initialValue}");
            Console.WriteLine($"Final Value: {finalValue}");
            Console.WriteLine($"Years: {years}");
            Console.WriteLine($"Expected CAGR: {expectedCAGR}");
            Console.WriteLine($"Actual CAGR: {actualCAGR}");
        }


        [TestMethod]
        public void CalculateSharpeRatio_Should_Work()
        {
            // Arrange
            var marketValueDict = GenerateFakeMarketValueDict();
            var riskFreeRate = 0.02m; // 2%
            var returns = marketValueDict.Values.Zip(marketValueDict.Values.Skip(1), (prev, curr) => curr - prev).ToList();
            var averageReturn = returns.Average();
            var standardDeviation = (decimal)Math.Sqrt(returns.Select(r => Math.Pow((double)r - (double)averageReturn, 2)).Average());
            var expectedSharpeRatio = (averageReturn - riskFreeRate) / standardDeviation;

            // Act
            var actualSharpeRatio = StrategyPerformanceAnalyzer.CalculateSharpeRatio(marketValueDict, riskFreeRate);

            // Assert
            Assert.AreEqual(expectedSharpeRatio, actualSharpeRatio, "Sharpe Ratio calculation is incorrect.");
        }


        [TestMethod]
        public void CalculateCalmarRatio_Should_Work()
        {
            // Arrange
            var marketValueDict = GenerateFakeMarketValueDict();
            var annualReturn = StrategyPerformanceAnalyzer.CalculateCAGR(marketValueDict);
            var maxDrawdown = StrategyPerformanceAnalyzer.CalculateMaximumDrawdown(marketValueDict.Values.ToList());
            var expectedCalmarRatio = maxDrawdown == 0 ? 0 : annualReturn / maxDrawdown;

            // Act
            var actualCalmarRatio = StrategyPerformanceAnalyzer.CalculateCalmarRatio(marketValueDict);

            // Assert
            Assert.AreEqual(expectedCalmarRatio, actualCalmarRatio, "Calmar Ratio calculation is incorrect.");
        }


        [TestMethod]
        public void CalculateMaximumDrawdown_Should_Work()
        {
            // Arrange
            var values = GenerateFakeMarketValueDict().Values.ToList();
            var peak = values[0];
            decimal maxDrawdown = 0;

            foreach (var value in values)
            {
                if (value > peak)
                {
                    peak = value;
                }

                var drawdown = (peak - value) / peak;
                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }

            // Act
            var actualMaxDrawdown = StrategyPerformanceAnalyzer.CalculateMaximumDrawdown(values);

            // Assert
            Assert.AreEqual(maxDrawdown, actualMaxDrawdown, "Maximum Drawdown calculation is incorrect.");
        }


        [TestMethod]
        public void CalculateMaxDrawdownDuration_Should_Work()
        {
            // Arrange
            var values = GenerateFakeMarketValueDict().Values.ToList();
            decimal peak = values[0];
            decimal maxDrawdown = 0;
            int maxDrawdownDuration = 0;
            int currentDrawdownDuration = 0;

            foreach (var value in values)
            {
                if (value > peak)
                {
                    peak = value;
                    currentDrawdownDuration = 0;
                }
                else
                {
                    currentDrawdownDuration++;
                    var drawdown = (peak - value) / peak;
                    if (drawdown > maxDrawdown)
                    {
                        maxDrawdown = drawdown;
                        maxDrawdownDuration = currentDrawdownDuration;
                    }
                }
            }

            // Act
            var (actualMaxDrawdown, actualMaxDrawdownDuration) = StrategyPerformanceAnalyzer.CalculateMaxDrawdownDuration(values);

            // Assert
            Assert.AreEqual(maxDrawdown, actualMaxDrawdown, "Maximum Drawdown calculation is incorrect.");
            Assert.AreEqual(maxDrawdownDuration, actualMaxDrawdownDuration, "Maximum Drawdown Duration calculation is incorrect.");
        }


        // 私有方法来生成假数据
        private Dictionary<DateTime, decimal> GenerateFakeMarketValueDict()
        {
            return new Dictionary<DateTime, decimal>
            {
                { new DateTime(2023, 1, 1), 10000m },
                { new DateTime(2023, 2, 1), 10500m },
                { new DateTime(2023, 3, 1), 11000m },
                { new DateTime(2023, 4, 1), 11500m },
                { new DateTime(2023, 5, 1), 12000m },
                { new DateTime(2023, 6, 1), 11000m },
                { new DateTime(2023, 7, 1), 10000m },
                { new DateTime(2023, 8, 1), 10500m },
                { new DateTime(2023, 9, 1), 10200m },
                { new DateTime(2023, 10, 1), 10800m }
            };
        }
    }
}
