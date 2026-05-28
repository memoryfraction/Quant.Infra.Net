using Microsoft.Extensions.Configuration;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Broker.Service;

namespace Quant.Infra.Net.Tests
{
    /// <summary>
    /// Integration tests for the Schwab broker service.
    /// Schwab 券商服务的集成测试。
    /// </summary>
    [TestClass]
    public class SchwabBrokerServiceTests
    {
        private ISchwabBrokerService _schwabService = null!;
        private IConfiguration _config = null!;

        /// <summary>
        /// Loads Schwab test configuration.
        /// 加载 Schwab 测试配置。
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            // Load configuration.
            _config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<SchwabBrokerServiceTests>()
                .Build();

            // Read Schwab credentials from configuration.
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

        /// <summary>
        /// Gets Schwab account data.
        /// 获取 Schwab 账户数据。
        /// </summary>
        [TestMethod]
        public async Task Test_GetAccount()
        {
            // Act
            var account = await _schwabService.GetAccountAsync();

            // Assert
            Assert.IsNotNull(account);
            Assert.IsFalse(string.IsNullOrEmpty(account.AccountNumber));
            Assert.IsTrue(account.TotalEquity > 0);

            Console.WriteLine($"Account number: {account.AccountNumber}");
            Console.WriteLine($"Account type: {account.AccountType}");
            Console.WriteLine($"Total equity: ${account.TotalEquity:N2}");
            Console.WriteLine($"Market value: ${account.MarketValue:N2}");
            Console.WriteLine($"Cash balance: ${account.CashBalance:N2}");
            Console.WriteLine($"Buying power: ${account.BuyingPower:N2}");
        }

        /// <summary>
        /// Gets Schwab account positions.
        /// 获取 Schwab 账户持仓。
        /// </summary>
        [TestMethod]
        public async Task Test_GetPositions()
        {
            // Act
            var positions = await _schwabService.GetPositionsAsync();

            // Assert
            Assert.IsNotNull(positions);
            Console.WriteLine($"Position count: {positions.Count}");

            foreach (var position in positions)
            {
                Console.WriteLine($"Symbol: {position.Symbol}");
                Console.WriteLine($"  Quantity: {position.Quantity}");
                Console.WriteLine($"  Cost price: ${position.CostPrice:N2}");
                Console.WriteLine($"  Asset type: {position.AssetType}");
                if (position.UnrealizedProfitLoss.HasValue)
                {
                    Console.WriteLine($"  Unrealized P/L: ${position.UnrealizedProfitLoss.Value:N2}");
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Gets one Schwab position by symbol.
        /// 按标的代码获取单个 Schwab 持仓。
        /// </summary>
        [TestMethod]
        public async Task Test_GetPosition_SpecificSymbol()
        {
            // Arrange
            var symbol = "AAPL"; // Test Apple stock.

            // Act
            var position = await _schwabService.GetPositionAsync(symbol);

            // Assert
            if (position != null)
            {
                Console.WriteLine($"Found {symbol} position:");
                Console.WriteLine($"  Quantity: {position.Quantity}");
                Console.WriteLine($"  Cost price: ${position.CostPrice:N2}");
            }
            else
            {
                Console.WriteLine($"No {symbol} position found");
            }
        }

        /// <summary>
        /// Gets one Schwab quote.
        /// 获取单个 Schwab 报价。
        /// </summary>
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

            Console.WriteLine($"{symbol} quote:");
            Console.WriteLine($"  Last price: ${quote.LastPrice:N2}");
            Console.WriteLine($"  Bid: ${quote.BidPrice:N2}");
            Console.WriteLine($"  Ask: ${quote.AskPrice:N2}");
            Console.WriteLine($"  Open: ${quote.Open:N2}");
            Console.WriteLine($"  High: ${quote.High:N2}");
            Console.WriteLine($"  Low: ${quote.Low:N2}");
            Console.WriteLine($"  Close: ${quote.Close:N2}");
            Console.WriteLine($"  Change: ${quote.Change:N2}");
            Console.WriteLine($"  Change percent: {quote.ChangePercent:N2}%");
            Console.WriteLine($"  Volume: {quote.Volume:N0}");
        }

        /// <summary>
        /// Gets multiple Schwab quotes.
        /// 批量获取多个 Schwab 报价。
        /// </summary>
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

            Console.WriteLine($"Loaded {quotes.Count} quotes:");
            foreach (var kvp in quotes)
            {
                Console.WriteLine($"{kvp.Key}: ${kvp.Value.LastPrice:N2} ({kvp.Value.ChangePercent:+0.00;-0.00}%)");
            }
        }

        /// <summary>
        /// Gets a Schwab option chain.
        /// 获取 Schwab 期权链。
        /// </summary>
        [TestMethod]
        public async Task Test_GetOptionChain()
        {
            // Arrange
            var symbol = "AAPL";
            var strikeCount = 5; // Load five strikes only.

            // Act
            var optionChain = await _schwabService.GetOptionChainAsync(symbol, strikeCount: strikeCount);

            // Assert
            Assert.IsNotNull(optionChain);
            Assert.AreEqual(symbol, optionChain.Symbol);
            Assert.IsTrue(optionChain.UnderlyingPrice > 0);

            Console.WriteLine($"{symbol} option chain:");
            Console.WriteLine($"  Underlying price: ${optionChain.UnderlyingPrice:N2}");
            Console.WriteLine($"  Call count: {optionChain.CallOptions.Count}");
            Console.WriteLine($"  Put count: {optionChain.PutOptions.Count}");

            // Show the first five call contracts.
            Console.WriteLine("\nFirst five call contracts:");
            foreach (var call in optionChain.CallOptions.Take(5))
            {
                Console.WriteLine($"  {call.Symbol}");
                Console.WriteLine($"    Expiration: {call.ExpirationDate}");
                Console.WriteLine($"    Strike: ${call.Strike:N2}");
                Console.WriteLine($"    Bid/Ask: ${call.Bid:N2} / ${call.Ask:N2}");
                Console.WriteLine($"    Last price: ${call.Last:N2}");
                Console.WriteLine($"    Implied volatility: {call.ImpliedVolatility:P2}");
                Console.WriteLine($"    Delta: {call.Delta:N4}");
                Console.WriteLine($"    Volume: {call.Volume:N0}");
                Console.WriteLine($"    Open interest: {call.OpenInterest:N0}");
                Console.WriteLine($"    In the money: {(call.InTheMoney ? "yes" : "no")}");
                Console.WriteLine();
            }

            // Show the first five put contracts.
            Console.WriteLine("First five put contracts:");
            foreach (var put in optionChain.PutOptions.Take(5))
            {
                Console.WriteLine($"  {put.Symbol}");
                Console.WriteLine($"    Expiration: {put.ExpirationDate}");
                Console.WriteLine($"    Strike: ${put.Strike:N2}");
                Console.WriteLine($"    Bid/Ask: ${put.Bid:N2} / ${put.Ask:N2}");
                Console.WriteLine($"    Last price: ${put.Last:N2}");
                Console.WriteLine($"    Implied volatility: {put.ImpliedVolatility:P2}");
                Console.WriteLine($"    Delta: {put.Delta:N4}");
                Console.WriteLine($"    Volume: {put.Volume:N0}");
                Console.WriteLine($"    Open interest: {put.OpenInterest:N0}");
                Console.WriteLine($"    In the money: {(put.InTheMoney ? "yes" : "no")}");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Gets call-only Schwab option chains.
        /// 获取仅包含看涨期权的 Schwab 期权链。
        /// </summary>
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
            Assert.AreEqual(0, optionChain.PutOptions.Count); // Only calls were requested, so puts should be empty.

            Console.WriteLine($"{symbol} call option chain:");
            Console.WriteLine($"  Underlying price: ${optionChain.UnderlyingPrice:N2}");
            Console.WriteLine($"  Call count: {optionChain.CallOptions.Count}");
        }

        /// <summary>
        /// Gets Schwab market status.
        /// 获取 Schwab 市场状态。
        /// </summary>
        [TestMethod]
        public async Task Test_IsMarketOpen()
        {
            // Act
            var isOpen = await _schwabService.IsMarketOpenAsync();

            // Assert
            Console.WriteLine($"Market status: {(isOpen ? "open" : "closed")}");
        }

        /// <summary>
        /// Places a live Schwab market buy order.
        /// 提交 Schwab 真实市价买入订单。
        /// </summary>
        [TestMethod]
        [Ignore] // Ignored by default to avoid accidental live orders.
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
            Console.WriteLine($"Order submitted. Order ID: {orderId}");

            // Wait briefly before querying order status.
            await Task.Delay(2000);
            var order = await _schwabService.GetOrderAsync(orderId);
            Console.WriteLine($"Order status: {order.Status}");
        }

        /// <summary>
        /// Gets recent Schwab orders.
        /// 获取 Schwab 最近订单。
        /// </summary>
        [TestMethod]
        public async Task Test_GetOrders()
        {
            // Act
            var orders = await _schwabService.GetOrdersAsync(maxResults: 10);

            // Assert
            Assert.IsNotNull(orders);
            Console.WriteLine($"Latest {orders.Count} orders:");

            foreach (var order in orders)
            {
                Console.WriteLine($"Order ID: {order.OrderId}");
                Console.WriteLine($"  Symbol: {order.Symbol}");
                Console.WriteLine($"  Side: {order.Side}");
                Console.WriteLine($"  Quantity: {order.Quantity}");
                Console.WriteLine($"  Filled: {order.FilledQuantity}");
                Console.WriteLine($"  Status: {order.Status}");
                Console.WriteLine($"  Order type: {order.OrderType}");
                Console.WriteLine($"  Created at: {order.CreatedAt}");
                if (order.AverageFilledPrice.HasValue)
                {
                    Console.WriteLine($"  Average fill price: ${order.AverageFilledPrice.Value:N2}");
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Gets positions enriched with real-time quotes.
        /// 获取带实时报价的持仓详情。
        /// </summary>
        [TestMethod]
        public async Task Test_GetPositions_WithQuotes()
        {
            // Act
            var positions = await _schwabService.GetPositionsAsync();
            
            if (positions.Count == 0)
            {
                Console.WriteLine("No positions found.");
                return;
            }

            var symbols = positions.Select(p => p.Symbol).ToList();
            var quotes = await _schwabService.GetQuotesAsync(symbols);

            // Assert
            Console.WriteLine("Position details with real-time quotes:");
            Console.WriteLine(new string('-', 100));
            Console.WriteLine($"{"Symbol",-10} {"Quantity",10} {"Cost",12} {"Last",12} {"Market Value",15} {"P/L",15} {"P/L %",10}");
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
            Console.WriteLine($"{"Total",-10} {"",-10} ${totalCost,10:N2} {"",-12} ${totalMarketValue,13:N2} ${totalPnl,13:N2} {totalPnlPercent,9:N2}%");
        }

        /// <summary>
        /// Tests the Schwab OAuth authorization flow manually.
        /// 手动测试 Schwab OAuth 授权流程。
        /// </summary>
        [TestMethod]
        [Ignore] // Ignored by default as it requires manual interaction.
        public void Test_Schwab_Authorization_Flow_Manual()
        {
            // This test demonstrates how to generate the authorization URL and handle the token exchange.
            // In a real scenario, you would open the URL in a browser, log in, and paste the code back here.
            
            var appKey = _config["Schwab:ApiKey"];
            var redirectUri = "https://127.0.0.1"; // Must match the one registered in Schwab Developer Portal.

            var authUrl = "https://api.schwabapi.com/v1/oauth/authorize"
                + $"?response_type=code"
                + $"&client_id={Uri.EscapeDataString(appKey)}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";

            Console.WriteLine("Please open the following URL in your browser to authorize:");
            Console.WriteLine(authUrl);
            Console.WriteLine("\nAfter authorization, you will be redirected. Copy the 'code' parameter from the URL.");
            Console.WriteLine("Then use that code to call ExchangeCodeForTokenAsync (internal logic of SchwabBrokerService).");
        }
    }
}


