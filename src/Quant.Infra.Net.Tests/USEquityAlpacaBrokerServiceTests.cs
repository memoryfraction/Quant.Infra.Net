using Microsoft.Extensions.Configuration;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Service;

namespace Quant.Infra.Net.Tests
{
    /// <summary>
    /// 用于测试美股 Alpaca 经纪服务的集成测试类。
    /// Integration test class for U.S. equity Alpaca broker service.
    /// </summary>
    [TestClass]
    public class USEquityAlpacaBrokerServiceTests
    {
        private IUSEquityBrokerService _broker;
        private readonly IConfiguration _configuration;
        private const string Symbol = "AAPL"; // 确保 Paper 账户中支持该股票

        /// <summary>
        /// 构造函数：加载配置文件及 user secrets，并初始化经纪服务实例。
        /// Constructor: loads config and initializes broker service.
        /// </summary>
        public USEquityAlpacaBrokerServiceTests()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<USEquityAlpacaBrokerServiceTests>()
                .Build();
            _configuration = config;
            _broker = new USEquityAlpacaBrokerService(config);
        }

        /// <summary>
        /// 测试：调用 SetHoldings 应该成功建立一个仓位。
        /// Test: SetHoldings should successfully open a position.
        /// </summary>
        [TestMethod]
        public async Task SetHoldings_ShouldIncreasePosition()
        {
            await _broker.SetHoldingsAsync(Symbol, 0.05);
            await Task.Delay(1000); // 等待Alpaca API操作完成
            var hasPosition = await _broker.HasPositionAsync(Symbol);
            await Task.Delay(1000); // 等待Alpaca API操作完成
            Assert.IsTrue(hasPosition);

            await _broker.LiquidateAsync(Symbol);
        }

        /// <summary>
        /// 测试：HasPosition 应准确识别持仓状态。
        /// Test: HasPosition should detect open and cleared position correctly.
        /// </summary>
        [TestMethod]
        public async Task HasPosition_ShouldDetectPosition()
        {
            // 下单建仓（0.05 股）
            await _broker.SetHoldingsAsync(Symbol, 0.05);

            // 最多等待 10 秒，轮询是否建立仓位
            bool hasPosition = false;
            for (int i = 0; i < 10; i++)
            {
                hasPosition = await _broker.HasPositionAsync(Symbol);
                if (hasPosition)
                    break;
                await Task.Delay(1000); // 每秒检查一次
            }

            Assert.IsTrue(hasPosition, "Position was not established — check if order was filled, market open, or symbol is tradable.");

            // 清仓
            await _broker.LiquidateAsync(Symbol);

            // 再次轮询确认清仓
            bool hasPositionAfter = true;
            for (int i = 0; i < 10; i++)
            {
                hasPositionAfter = await _broker.HasPositionAsync(Symbol);
                if (!hasPositionAfter)
                    break;
                await Task.Delay(1000);
            }

            Assert.IsFalse(hasPositionAfter, "Position was not cleared — Liquidate may have failed or is delayed.");
        }


        /// <summary>
        /// 测试：GetPortfolioMarketValue 应返回正值。
        /// Test: GetPortfolioMarketValue should return a positive number.
        /// </summary>
        [TestMethod]
        public async Task GetPortfolioMarketValue_ShouldReturnPositiveValue()
        {
            var value = await _broker.GetPortfolioMarketValueAsync();
            Console.WriteLine($"Portfolio market value: {value}");
            Assert.IsTrue(value > 0, "Portfolio market value should be greater than 0.");
        }

        /// <summary>
        /// 测试：未实现盈亏比率应在合理范围内（-100% ~ +100%）。
        /// Test: Unrealized profit/loss rate should be within -100% to 100%.
        /// </summary>
        [TestMethod]
        public async Task GetUnrealizedProfitRate_ShouldBeWithinRange()
        {
            var rate = await _broker.GetUnrealizedProfitRateAsync();
            Console.WriteLine($"Unrealized profit/loss rate: {rate}");
            Assert.IsTrue(rate > -1.0 && rate < 1.0, "Unrealized PnL rate should be within -100% ~ 100%");
        }

        /// <summary>
        /// 测试：Liquidate 应能清除指定标的的持仓。
        /// Test: Liquidate should close the position successfully.
        /// </summary>
        [TestMethod]
        public async Task Liquidate_ShouldClearPosition()
        {
            await _broker.SetHoldingsAsync(Symbol, 0.05);
            await Task.Delay(2000); // 等待Alpaca API操作完成
            var hasPosition = await _broker.HasPositionAsync(Symbol);
            Assert.IsTrue(hasPosition);

            await _broker.LiquidateAsync(Symbol);
            await Task.Delay(1000); // 等待Alpaca API操作完成
            var cleared = await _broker.HasPositionAsync(Symbol);
            Assert.IsFalse(cleared);
        }
    }
}
