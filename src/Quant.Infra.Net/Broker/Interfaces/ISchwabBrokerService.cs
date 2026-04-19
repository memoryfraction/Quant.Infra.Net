using Quant.Infra.Net.Portfolio.Models;
using Quant.Infra.Net.Shared.Model;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Interfaces
{
    /// <summary>
    /// Charles Schwab 券商服务接口
    /// Interface for Charles Schwab broker services
    /// </summary>
    public interface ISchwabBrokerService
    {
        /// <summary>
        /// 获取账户信息
        /// Get account information
        /// </summary>
        Task<SchwabAccount> GetAccountAsync();

        /// <summary>
        /// 获取所有持仓信息
        /// Get all positions
        /// </summary>
        Task<List<Position>> GetPositionsAsync();

        /// <summary>
        /// 获取指定标的的持仓信息
        /// Get position for a specific symbol
        /// </summary>
        Task<Position?> GetPositionAsync(string symbol);

        /// <summary>
        /// 获取股票报价
        /// Get stock quote
        /// </summary>
        Task<SchwabQuote> GetQuoteAsync(string symbol);

        /// <summary>
        /// 批量获取股票报价
        /// Get multiple stock quotes
        /// </summary>
        Task<Dictionary<string, SchwabQuote>> GetQuotesAsync(List<string> symbols);

        /// <summary>
        /// 获取期权链
        /// Get option chain for a symbol
        /// </summary>
        Task<SchwabOptionChain> GetOptionChainAsync(string symbol, string? contractType = null, int? strikeCount = null);

        /// <summary>
        /// 下单
        /// Place an order
        /// </summary>
        Task<string> PlaceOrderAsync(SchwabOrderRequest order);

        /// <summary>
        /// 获取订单状态
        /// Get order status
        /// </summary>
        Task<SchwabOrder> GetOrderAsync(string orderId);

        /// <summary>
        /// 取消订单
        /// Cancel an order
        /// </summary>
        Task<bool> CancelOrderAsync(string orderId);

        /// <summary>
        /// 获取账户的所有订单
        /// Get all orders for the account
        /// </summary>
        Task<List<SchwabOrder>> GetOrdersAsync(int maxResults = 100);

        /// <summary>
        /// 获取市场是否开盘
        /// Check if market is open
        /// </summary>
        Task<bool> IsMarketOpenAsync();
    }

    #region Schwab Models

    /// <summary>
    /// Schwab 账户信息
    /// </summary>
    public class SchwabAccount
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal CashBalance { get; set; }
        public decimal MarketValue { get; set; }
        public decimal TotalEquity { get; set; }
        public decimal BuyingPower { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
    }

    /// <summary>
    /// Schwab 股票报价
    /// </summary>
    public class SchwabQuote
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal BidPrice { get; set; }
        public decimal AskPrice { get; set; }
        public decimal LastPrice { get; set; }
        public long Volume { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// Schwab 期权链
    /// </summary>
    public class SchwabOptionChain
    {
        public string Symbol { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal UnderlyingPrice { get; set; }
        public List<SchwabOptionContract> CallOptions { get; set; } = new();
        public List<SchwabOptionContract> PutOptions { get; set; } = new();
    }

    /// <summary>
    /// Schwab 期权合约
    /// </summary>
    public class SchwabOptionContract
    {
        public string Symbol { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ExpirationDate { get; set; } = string.Empty;
        public decimal Strike { get; set; }
        public string ContractType { get; set; } = string.Empty; // CALL or PUT
        public decimal Bid { get; set; }
        public decimal Ask { get; set; }
        public decimal Last { get; set; }
        public decimal Mark { get; set; }
        public long Volume { get; set; }
        public long OpenInterest { get; set; }
        public decimal ImpliedVolatility { get; set; }
        public decimal Delta { get; set; }
        public decimal Gamma { get; set; }
        public decimal Theta { get; set; }
        public decimal Vega { get; set; }
        public decimal Rho { get; set; }
        public bool InTheMoney { get; set; }
    }

    /// <summary>
    /// Schwab 订单请求
    /// </summary>
    public class SchwabOrderRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string OrderType { get; set; } = "MARKET"; // MARKET, LIMIT, STOP, STOP_LIMIT
        public string Side { get; set; } = "BUY"; // BUY, SELL
        public int Quantity { get; set; }
        public decimal? LimitPrice { get; set; }
        public decimal? StopPrice { get; set; }
        public string TimeInForce { get; set; } = "DAY"; // DAY, GTC, IOC, FOK
        public string AssetType { get; set; } = "EQUITY"; // EQUITY, OPTION
    }

    /// <summary>
    /// Schwab 订单
    /// </summary>
    public class SchwabOrder
    {
        public string OrderId { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string Side { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int FilledQuantity { get; set; }
        public decimal? LimitPrice { get; set; }
        public decimal? StopPrice { get; set; }
        public decimal? AverageFilledPrice { get; set; }
        public string TimeInForce { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }

    #endregion
}
