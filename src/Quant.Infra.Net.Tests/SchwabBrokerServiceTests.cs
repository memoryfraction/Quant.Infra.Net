using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Broker.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class SchwabBrokerServiceTests
    {
        private ISchwabBrokerService _schwabService = null!;
        private IConfiguration _config = null!;

        [TestInitialize]
        public void Setup()
        {
            // 加载配置
            _config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.test.json", optional: true)
                .AddUserSecrets<SchwabBrokerServiceTests>()
                .Build();

            // 从配置中读取 Schwab 凭据
            var schwabConfig = _config.GetSection("Schwab");
            var credentials = new BrokerCredentials
            {
                ApiKey = schwabConfig["ApiKey"] ?? throw new InvalidOperationException("Schwab ApiKey not found"),
                Secret = schwabConfig["Secret"] ?? throw new InvalidOperationException("Schwab Secret not found"),
                BaseUrl = schwabConfig["BaseUrl"] ?? "https://api.schwabapi.com/trader/v1"
            };

            var accountNumber = schwabConfig["AccountNumber"] ?? throw new InvalidOperationException("Schwab AccountNumber not found");

            _schwabService = new SchwabBrokerService(credentials, accountNumber);
        }

        [TestMethod]
        public async Task Test_GetAccount()
        {
            // Act
            var account = await _schwabService.GetAccountAsync();

            // Assert
            Assert.IsNotNull(account);
            Assert.IsFalse(string.IsNullOrEmpty(account.AccountNumber));
            Assert.IsTrue(account.TotalEquity > 0);

            Console.WriteLine($"账户号码: {account.AccountNumber}");
            Console.WriteLine($"账户类型: {account.AccountType}");
            Console.WriteLine($"总资产: ${account.TotalEquity:N2}");
            Console.WriteLine($"市值: ${account.MarketValue:N2}");
            Console.WriteLine($"现金余额: ${account.CashBalance:N2}");
            Console.WriteLine($"购买力: ${account.BuyingPower:N2}");
        }

        [TestMethod]
        public async Task Test_GetPositions()
        {
            // Act
            var positions = await _schwabService.GetPositionsAsync();

            // Assert
            Assert.IsNotNull(positions);
            Console.WriteLine($"持仓数量: {positions.Count}");

            foreach (var position in positions)
            {
                Console.WriteLine($"标的: {position.Symbol}");
                Console.WriteLine($"  数量: {position.Quantity}");
                Console.WriteLine($"  成本价: ${position.CostPrice:N2}");
                Console.WriteLine($"  资产类型: {position.AssetType}");
                if (position.UnrealizedProfitLoss.HasValue)
                {
                    Console.WriteLine($"  未实现盈亏: ${position.UnrealizedProfitLoss.Value:N2}");
                }
                Console.WriteLine();
            }
        }

        [TestMethod]
        public async Task Test_GetPosition_SpecificSymbol()
        {
            // Arrange
            var symbol = "AAPL"; // 测试苹果股票

            // Act
            var position = await _schwabService.GetPositionAsync(symbol);

            // Assert
            if (position != null)
            {
                Console.WriteLine($"找到 {symbol} 持仓:");
                Console.WriteLine($"  数量: {position.Quantity}");
                Console.WriteLine($"  成本价: ${position.CostPrice:N2}");
            }
            else
            {
                Console.WriteLine($"未找到 {symbol} 持仓");
            }
        }

        [TestMethod]
        public async Task Test_GetQuote()
        {
            // Arrange
            var symbol = "AAPL";

            // Act
            var quote = await _schwabService.GetQuoteAsync(symbol);

            // Assert
            Assert.IsNotNull(quote);
            Assert.AreEqual(symbol, quote.Symbol);
            Assert.IsTrue(quote.LastPrice > 0);

            Console.WriteLine($"{symbol} 报价信息:");
            Console.WriteLine($"  最新价: ${quote.LastPrice:N2}");
            Console.WriteLine($"  买价: ${quote.BidPrice:N2}");
            Console.WriteLine($"  卖价: ${quote.AskPrice:N2}");
            Console.WriteLine($"  开盘价: ${quote.Open:N2}");
            Console.WriteLine($"  最高价: ${quote.High:N2}");
            Console.WriteLine($"  最低价: ${quote.Low:N2}");
            Console.WriteLine($"  收盘价: ${quote.Close:N2}");
            Console.WriteLine($"  涨跌额: ${quote.Change:N2}");
            Console.WriteLine($"  涨跌幅: {quote.ChangePercent:N2}%");
            Console.WriteLine($"  成交量: {quote.Volume:N0}");
        }

        [TestMethod]
        public async Task Test_GetQuotes_Multiple()
        {
            // Arrange
            var symbols = new List<string> { "AAPL", "MSFT", "GOOGL", "TSLA" };

            // Act
            var quotes = await _schwabService.GetQuotesAsync(symbols);

            // Assert
            Assert.IsNotNull(quotes);
            Assert.IsTrue(quotes.Count > 0);

            Console.WriteLine($"获取到 {quotes.Count} 个报价:");
            foreach (var kvp in quotes)
            {
                Console.WriteLine($"{kvp.Key}: ${kvp.Value.LastPrice:N2} ({kvp.Value.ChangePercent:+0.00;-0.00}%)");
            }
        }

        [TestMethod]
        public async Task Test_GetOptionChain()
        {
            // Arrange
            var symbol = "AAPL";
            var strikeCount = 5; // 只获取5个行权价

            // Act
            var optionChain = await _schwabService.GetOptionChainAsync(symbol, strikeCount: strikeCount);

            // Assert
            Assert.IsNotNull(optionChain);
            Assert.AreEqual(symbol, optionChain.Symbol);
            Assert.IsTrue(optionChain.UnderlyingPrice > 0);

            Console.WriteLine($"{symbol} 期权链:");
            Console.WriteLine($"  标的价格: ${optionChain.UnderlyingPrice:N2}");
            Console.WriteLine($"  Call 期权数量: {optionChain.CallOptions.Count}");
            Console.WriteLine($"  Put 期权数量: {optionChain.PutOptions.Count}");

            // 显示前5个 Call 期权
            Console.WriteLine("\n前5个 Call 期权:");
            foreach (var call in optionChain.CallOptions.Take(5))
            {
                Console.WriteLine($"  {call.Symbol}");
                Console.WriteLine($"    到期日: {call.ExpirationDate}");
                Console.WriteLine($"    行权价: ${call.Strike:N2}");
                Console.WriteLine($"    买价/卖价: ${call.Bid:N2} / ${call.Ask:N2}");
                Console.WriteLine($"    最新价: ${call.Last:N2}");
                Console.WriteLine($"    隐含波动率: {call.ImpliedVolatility:P2}");
                Console.WriteLine($"    Delta: {call.Delta:N4}");
                Console.WriteLine($"    成交量: {call.Volume:N0}");
                Console.WriteLine($"    未平仓合约: {call.OpenInterest:N0}");
                Console.WriteLine($"    价内: {(call.InTheMoney ? "是" : "否")}");
                Console.WriteLine();
            }

            // 显示前5个 Put 期权
            Console.WriteLine("前5个 Put 期权:");
            foreach (var put in optionChain.PutOptions.Take(5))
            {
                Console.WriteLine($"  {put.Symbol}");
                Console.WriteLine($"    到期日: {put.ExpirationDate}");
                Console.WriteLine($"    行权价: ${put.Strike:N2}");
                Console.WriteLine($"    买价/卖价: ${put.Bid:N2} / ${put.Ask:N2}");
                Console.WriteLine($"    最新价: ${put.Last:N2}");
                Console.WriteLine($"    隐含波动率: {put.ImpliedVolatility:P2}");
                Console.WriteLine($"    Delta: {put.Delta:N4}");
                Console.WriteLine($"    成交量: {put.Volume:N0}");
                Console.WriteLine($"    未平仓合约: {put.OpenInterest:N0}");
                Console.WriteLine($"    价内: {(put.InTheMoney ? "是" : "否")}");
                Console.WriteLine();
            }
        }

        [TestMethod]
        public async Task Test_GetOptionChain_CallsOnly()
        {
            // Arrange
            var symbol = "SPY";

            // Act
            var optionChain = await _schwabService.GetOptionChainAsync(symbol, contractType: "CALL", strikeCount: 3);

            // Assert
            Assert.IsNotNull(optionChain);
            Assert.IsTrue(optionChain.CallOptions.Count > 0);
            Assert.AreEqual(0, optionChain.PutOptions.Count); // 只请求 Call，Put 应该为空

            Console.WriteLine($"{symbol} Call 期权链:");
            Console.WriteLine($"  标的价格: ${optionChain.UnderlyingPrice:N2}");
            Console.WriteLine($"  Call 期权数量: {optionChain.CallOptions.Count}");
        }

        [TestMethod]
        public async Task Test_IsMarketOpen()
        {
            // Act
            var isOpen = await _schwabService.IsMarketOpenAsync();

            // Assert
            Console.WriteLine($"市场状态: {(isOpen ? "开盘" : "休市")}");
        }

        [TestMethod]
        [Ignore] // 默认忽略，避免意外下单
        public async Task Test_PlaceOrder_MarketBuy()
        {
            // Arrange
            var orderRequest = new SchwabOrderRequest
            {
                Symbol = "AAPL",
                OrderType = "MARKET",
                Side = "BUY",
                Quantity = 1,
                TimeInForce = "DAY",
                AssetType = "EQUITY"
            };

            // Act
            var orderId = await _schwabService.PlaceOrderAsync(orderRequest);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(orderId));
            Console.WriteLine($"订单已提交，订单ID: {orderId}");

            // 等待一下，然后查询订单状态
            await Task.Delay(2000);
            var order = await _schwabService.GetOrderAsync(orderId);
            Console.WriteLine($"订单状态: {order.Status}");
        }

        [TestMethod]
        public async Task Test_GetOrders()
        {
            // Act
            var orders = await _schwabService.GetOrdersAsync(maxResults: 10);

            // Assert
            Assert.IsNotNull(orders);
            Console.WriteLine($"最近 {orders.Count} 个订单:");

            foreach (var order in orders)
            {
                Console.WriteLine($"订单ID: {order.OrderId}");
                Console.WriteLine($"  标的: {order.Symbol}");
                Console.WriteLine($"  方向: {order.Side}");
                Console.WriteLine($"  数量: {order.Quantity}");
                Console.WriteLine($"  已成交: {order.FilledQuantity}");
                Console.WriteLine($"  状态: {order.Status}");
                Console.WriteLine($"  订单类型: {order.OrderType}");
                Console.WriteLine($"  创建时间: {order.CreatedAt}");
                if (order.AverageFilledPrice.HasValue)
                {
                    Console.WriteLine($"  成交均价: ${order.AverageFilledPrice.Value:N2}");
                }
                Console.WriteLine();
            }
        }

        [TestMethod]
        public async Task Test_GetPositions_WithQuotes()
        {
            // Act
            var positions = await _schwabService.GetPositionsAsync();
            
            if (positions.Count == 0)
            {
                Console.WriteLine("当前没有持仓");
                return;
            }

            var symbols = positions.Select(p => p.Symbol).ToList();
            var quotes = await _schwabService.GetQuotesAsync(symbols);

            // Assert
            Console.WriteLine("持仓详情（含实时报价）:");
            Console.WriteLine(new string('-', 100));
            Console.WriteLine($"{"标的",-10} {"数量",10} {"成本价",12} {"当前价",12} {"市值",15} {"盈亏",15} {"盈亏率",10}");
            Console.WriteLine(new string('-', 100));

            decimal totalCost = 0;
            decimal totalMarketValue = 0;

            foreach (var position in positions)
            {
                if (quotes.TryGetValue(position.Symbol, out var quote))
                {
                    var marketValue = position.Quantity * quote.LastPrice;
                    var costValue = position.Quantity * position.CostPrice;
                    var pnl = marketValue - costValue;
                    var pnlPercent = costValue != 0 ? (pnl / costValue) * 100 : 0;

                    totalCost += costValue;
                    totalMarketValue += marketValue;

                    Console.WriteLine($"{position.Symbol,-10} {position.Quantity,10:N2} ${position.CostPrice,10:N2} ${quote.LastPrice,10:N2} ${marketValue,13:N2} ${pnl,13:N2} {pnlPercent,9:N2}%");
                }
            }

            Console.WriteLine(new string('-', 100));
            var totalPnl = totalMarketValue - totalCost;
            var totalPnlPercent = totalCost != 0 ? (totalPnl / totalCost) * 100 : 0;
            Console.WriteLine($"{"总计",-10} {"",-10} ${totalCost,10:N2} {"",-12} ${totalMarketValue,13:N2} ${totalPnl,13:N2} {totalPnlPercent,9:N2}%");
        }
    }
}
