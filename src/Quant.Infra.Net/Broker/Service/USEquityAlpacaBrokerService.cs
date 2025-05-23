// 以下为 Alpaca 美股经纪服务类的带注释实现（中英文 XML 注释）
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Service
{
    /// <summary>
    /// 美股 Alpaca 经纪服务实现类。
    /// Broker service implementation for U.S. equities using Alpaca API.
    /// </summary>
    public class USEquityAlpacaBrokerService : IUSEquityBrokerService
    {
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly string _apiKey, _apiSecret;
        private readonly AlpacaClient _alpacaClient;
        /// <summary>
        /// 当前交易环境（实盘 / 模拟盘）。
        /// Current exchange environment (e.g., Live or Paper).
        /// </summary>
        public ExchangeEnvironment ExchangeEnvironment { get; set; }

        /// <summary>
        /// 构造函数，初始化 API 密钥与重试策略。
        /// Constructor that initializes API credentials and retry policy.
        /// </summary>
        /// <param name="configuration">配置文件接口。</param>
        public USEquityAlpacaBrokerService(IConfiguration configuration)
        {
            _apiKey = configuration["Exchange:ApiKey"];
            _apiSecret = configuration["Exchange:ApiSecret"];
            ExchangeEnvironment = (ExchangeEnvironment)Enum.Parse(typeof(ExchangeEnvironment), configuration["Exchange:Environment"].ToString());

            _alpacaClient = new AlpacaClient(new BrokerCredentials { ApiKey = _apiKey, Secret = _apiSecret }, ExchangeEnvironment);

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); // 指数退避策略
        }

        /// <summary>
        /// 获取当前投资组合的市值（USD）。
        /// Get the total market value of the current portfolio.
        /// </summary>
        public async Task<decimal> GetPortfolioMarketValueAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () => await _alpacaClient.GetAccountEquityAsync());
        }

        /// <summary>
        /// 获取未实现盈亏比率（总浮盈/浮亏）。
        /// Get unrealized profit/loss rate.
        /// </summary>
        public async Task<double> GetUnrealizedProfitRateAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var positions = await _alpacaClient.GetAllPositionsAsync();
                decimal totalCost = 0;
                decimal totalValue = 0;

                foreach (var pos in positions)
                {
                    // 注意：pos.Quantity 可为负，表示做空
                    totalCost += pos.AverageEntryPrice * pos.Quantity;
                    totalValue += pos.MarketValue.Value;
                }

                var pnl = totalValue - totalCost;
                var rate = totalCost != 0 ? (double)(pnl / Math.Abs(totalCost)) : 0.0;
                return rate;
            });
        }


        /// <summary>
        /// 检查指定股票是否持有仓位。
        /// Check if a position exists for the given symbol.
        /// </summary>
        /// <param name="symbol">股票代码，例如 AAPL。</param>
        public async Task<bool> HasPositionAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var position = await _alpacaClient.GetPositionAsync(symbol);
                return position != null && position.Quantity != 0;
            });
        }

        /// <summary>
        /// 平掉指定股票的所有持仓。
        /// Liquidate the position for the given symbol.
        /// </summary>
        /// <param name="symbol">要平仓的股票代码。</param>
        public async Task LiquidateAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");

            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _alpacaClient.ExitPositionAsync(symbol);
                return Task.CompletedTask;
            });
        }


        /// <summary>
        /// 设置指定股票的持仓比例（如 0.1 表示持仓市值占总资产 10%）。
        /// Set target position for a symbol as a percentage of total portfolio value.
        /// </summary>
        /// <param name="symbol">股票代码。</param>
        /// <param name="rate">目标持仓比例。</param>
        public async Task SetHoldingsAsync(string symbol, double rate)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");

            if (rate < -1.0 || rate > 1.0)
                throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be between -1.0 and 1.0.");

            var asset = await _alpacaClient.GetAssetAsync(symbol);
            if (!asset.IsTradable)
                throw new InvalidOperationException($"{symbol} is not tradable.");

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var asset = await _alpacaClient.GetAssetAsync(symbol);
                if (!asset.IsTradable)
                    throw new InvalidOperationException($"{symbol} is not tradable.");

                if (rate < 0 && !asset.Shortable)
                    throw new InvalidOperationException($"{symbol} is not shortable but a short position was requested.");

                var accountEquity = await _alpacaClient.GetAccountEquityAsync();
                var latestPrice = await _alpacaClient.GetLatestPriceAsync(symbol);

                var targetMarketValue = accountEquity * (decimal)rate;
                var targetShares = targetMarketValue / latestPrice;

                var existingPosition = await _alpacaClient.GetPositionAsync(symbol);
                var currentShares = existingPosition?.Quantity ?? 0m;
                var diffShares = targetShares - currentShares;

                if (Math.Abs(diffShares) < 0.0001m)
                    return; // 差距极小则忽略

                await _alpacaClient.PlaceOrderAsync(symbol, diffShares);
            });



        }
    }
}