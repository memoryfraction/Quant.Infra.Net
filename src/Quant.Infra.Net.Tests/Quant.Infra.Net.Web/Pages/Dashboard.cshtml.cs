using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Portfolio.Models;
using Quant.Infra.Net.Shared.Model;

namespace Quant.Infra.Net.Web.Pages;

/// <summary>
/// Schwab account dashboard page model.
/// Schwab 账户仪表板页面模型。
/// </summary>
public class DashboardModel : PageModel
{
    private readonly ISchwabBrokerService _schwabService;
    private readonly ILogger<DashboardModel> _logger;

    /// <summary>
    /// Loaded Schwab account summary.
    /// 已加载的 Schwab 账户摘要。
    /// </summary>
    public SchwabAccount? Account { get; set; }

    /// <summary>
    /// Loaded portfolio positions.
    /// 已加载的持仓列表。
    /// </summary>
    public List<Position>? Positions { get; set; }

    /// <summary>
    /// Quote returned by the latest quote request.
    /// 最近一次报价请求返回的报价。
    /// </summary>
    public SchwabQuote? Quote { get; set; }

    /// <summary>
    /// Option chain returned by the latest option-chain request.
    /// 最近一次期权链请求返回的期权链。
    /// </summary>
    public SchwabOptionChain? OptionChain { get; set; }

    /// <summary>
    /// Loaded account orders.
    /// 已加载的账户订单。
    /// </summary>
    public List<SchwabOrder>? Orders { get; set; }

    /// <summary>
    /// User-visible error message.
    /// 用户可见的错误消息。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the dashboard is using simulated data.
    /// 仪表板是否正在使用模拟数据。
    /// </summary>
    public bool IsDemo { get; set; }

    /// <summary>
    /// Active dashboard tab id.
    /// 当前激活的仪表板页签标识。
    /// </summary>
    public string ActiveTab { get; set; } = "account";

    /// <summary>
    /// Creates a Schwab dashboard page model.
    /// 创建 Schwab 仪表板页面模型。
    /// </summary>
    public DashboardModel(ISchwabBrokerService schwabService, ILogger<DashboardModel> logger)
    {
        _schwabService = schwabService;
        _logger = logger;
    }

    /// <summary>
    /// Loads dashboard account data.
    /// 加载仪表板账户数据。
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (HttpContext.Session.GetString("AccountNumber") == null)
            return RedirectToPage("/Index");

        if (HttpContext.Session.GetString("DemoMode") == "true")
        {
            LoadDemoData();
            return Page();
        }

        await LoadAccountDataAsync();
        return Page();
    }

    /// <summary>
    /// Loads a quote and keeps the quote tab active.
    /// 加载报价并保持报价页签处于激活状态。
    /// </summary>
    public async Task<IActionResult> OnPostGetQuoteAsync(string symbol)
    {
        ActiveTab = "quotes";

        if (HttpContext.Session.GetString("DemoMode") == "true")
        {
            LoadDemoData();
            Quote = GetDemoQuote(symbol.ToUpperInvariant());
            return Page();
        }

        await LoadAccountDataAsync();

        try
        {
            Quote = await _schwabService.GetQuoteAsync(symbol.ToUpperInvariant());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quote for {Symbol}", symbol);
            ErrorMessage = $"Failed to get quote: {ex.Message}";
        }

        return Page();
    }

    /// <summary>
    /// Loads an option chain and keeps the options tab active.
    /// 加载期权链并保持期权页签处于激活状态。
    /// </summary>
    public async Task<IActionResult> OnPostGetOptionChainAsync(string symbol)
    {
        ActiveTab = "options";

        if (HttpContext.Session.GetString("DemoMode") == "true")
        {
            LoadDemoData();
            OptionChain = GetDemoOptionChain(symbol.ToUpperInvariant());
            return Page();
        }

        await LoadAccountDataAsync();

        try
        {
            OptionChain = await _schwabService.GetOptionChainAsync(symbol.ToUpperInvariant(), strikeCount: 10);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting option chain for {Symbol}", symbol);
            ErrorMessage = $"Failed to get option chain: {ex.Message}";
        }

        return Page();
    }

    /// <summary>
    /// Clears the current Schwab session.
    /// 清除当前 Schwab 会话。
    /// </summary>
    public IActionResult OnPostLogout()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Index");
    }

    private async Task LoadAccountDataAsync()
    {
        try
        {
            Account = await _schwabService.GetAccountAsync();
            Positions = await _schwabService.GetPositionsAsync();
            Orders = await TryLoadOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data");
            ErrorMessage = $"Failed to load dashboard data: {ex.Message}";
            Account = null;
            Positions = new List<Position>();
            Orders = new List<SchwabOrder>();
        }
    }

    private async Task<List<SchwabOrder>> TryLoadOrdersAsync()
    {
        try
        {
            return await _schwabService.GetOrdersAsync(50);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load Schwab orders");
            return new List<SchwabOrder>();
        }
    }

    private void LoadDemoData()
    {
        IsDemo = true;
        Account = new SchwabAccount
        {
            AccountNumber = "****8583",
            AccountType = "Individual",
            CashBalance = 25430.50m,
            MarketValue = 98750.00m,
            TotalEquity = 124180.50m,
            BuyingPower = 50861.00m,
            UnrealizedPnL = 3240.75m,
            RealizedPnL = 1850.00m
        };

        Positions = new List<Position>
        {
            new() { Symbol = "AAPL", Quantity = 100, CostPrice = 165.20m, AssetType = AssetType.UsEquity, UnrealizedProfitLoss = 2080.00m },
            new() { Symbol = "MSFT", Quantity = 50, CostPrice = 310.50m, AssetType = AssetType.UsEquity, UnrealizedProfitLoss = 1225.00m },
            new() { Symbol = "GOOGL", Quantity = 20, CostPrice = 140.80m, AssetType = AssetType.UsEquity, UnrealizedProfitLoss = 364.00m },
            new() { Symbol = "NVDA", Quantity = 30, CostPrice = 480.00m, AssetType = AssetType.UsEquity, UnrealizedProfitLoss = -1350.00m },
            new() { Symbol = "TSLA", Quantity = 25, CostPrice = 220.00m, AssetType = AssetType.UsEquity, UnrealizedProfitLoss = 875.00m },
            new() { Symbol = "SPY", Quantity = 40, CostPrice = 490.00m, AssetType = AssetType.UsEquity, UnrealizedProfitLoss = 480.00m },
        };

        Orders = new List<SchwabOrder>
        {
            new() { OrderId = "DEMO-1001", Symbol = "AAPL", Status = "FILLED", OrderType = "LIMIT", Side = "BUY", Quantity = 10, FilledQuantity = 10, LimitPrice = 185.00m, AverageFilledPrice = 184.92m, TimeInForce = "DAY", CreatedAt = "2026-05-08T14:31:00Z" },
            new() { OrderId = "DEMO-1002", Symbol = "MSFT", Status = "CANCELED", OrderType = "LIMIT", Side = "SELL", Quantity = 5, FilledQuantity = 0, LimitPrice = 340.00m, TimeInForce = "DAY", CreatedAt = "2026-05-07T15:10:00Z" },
        };
    }

    private static SchwabQuote GetDemoQuote(string symbol) => symbol switch
    {
        "AAPL" => new SchwabQuote { Symbol = "AAPL", LastPrice = 185.92m, BidPrice = 185.90m, AskPrice = 185.94m, Change = 1.32m, ChangePercent = 0.71m, High = 186.50m, Low = 184.20m, Volume = 52_340_000 },
        "MSFT" => new SchwabQuote { Symbol = "MSFT", LastPrice = 334.75m, BidPrice = 334.70m, AskPrice = 334.80m, Change = 2.15m, ChangePercent = 0.65m, High = 335.20m, Low = 331.80m, Volume = 18_920_000 },
        "GOOGL" => new SchwabQuote { Symbol = "GOOGL", LastPrice = 158.92m, BidPrice = 158.88m, AskPrice = 158.96m, Change = -0.48m, ChangePercent = -0.30m, High = 160.10m, Low = 158.20m, Volume = 22_100_000 },
        "NVDA" => new SchwabQuote { Symbol = "NVDA", LastPrice = 435.00m, BidPrice = 434.90m, AskPrice = 435.10m, Change = -5.50m, ChangePercent = -1.25m, High = 442.00m, Low = 433.50m, Volume = 41_500_000 },
        "TSLA" => new SchwabQuote { Symbol = "TSLA", LastPrice = 255.00m, BidPrice = 254.90m, AskPrice = 255.10m, Change = 3.20m, ChangePercent = 1.27m, High = 256.80m, Low = 250.40m, Volume = 88_200_000 },
        _ => new SchwabQuote { Symbol = symbol, LastPrice = 100.00m, BidPrice = 99.95m, AskPrice = 100.05m, Change = 0.50m, ChangePercent = 0.50m, High = 101.00m, Low = 99.00m, Volume = 5_000_000 }
    };

    private static SchwabOptionChain GetDemoOptionChain(string symbol)
    {
        var price = symbol == "AAPL" ? 185.92m : 100.00m;
        var calls = new List<SchwabOptionContract>();
        var puts = new List<SchwabOptionContract>();

        decimal[] strikes = { price - 10, price - 5, price, price + 5, price + 10 };
        string[] expiries = { "2026-05-15", "2026-06-19", "2026-07-17" };

        foreach (var exp in expiries)
        {
            foreach (var strike in strikes)
            {
                var itm = strike < price;
                calls.Add(new SchwabOptionContract
                {
                    Symbol = $"{symbol}_{exp}_C{strike:F0}",
                    ExpirationDate = exp,
                    Strike = strike,
                    ContractType = "CALL",
                    InTheMoney = itm,
                    Bid = 3.20m,
                    Ask = 3.40m,
                    Last = 3.30m,
                    Volume = 1200,
                    OpenInterest = 8500,
                    Delta = itm ? 0.72m : 0.35m,
                    ImpliedVolatility = 0.28m,
                    Gamma = 0.02m,
                    Theta = -0.05m,
                    Vega = 0.15m
                });
                puts.Add(new SchwabOptionContract
                {
                    Symbol = $"{symbol}_{exp}_P{strike:F0}",
                    ExpirationDate = exp,
                    Strike = strike,
                    ContractType = "PUT",
                    InTheMoney = !itm,
                    Bid = 2.80m,
                    Ask = 3.00m,
                    Last = 2.90m,
                    Volume = 980,
                    OpenInterest = 6200,
                    Delta = itm ? -0.28m : -0.65m,
                    ImpliedVolatility = 0.30m,
                    Gamma = 0.02m,
                    Theta = -0.04m,
                    Vega = 0.14m
                });
            }
        }

        return new SchwabOptionChain
        {
            Symbol = symbol,
            Status = "SUCCESS",
            UnderlyingPrice = price,
            CallOptions = calls,
            PutOptions = puts
        };
    }
}
