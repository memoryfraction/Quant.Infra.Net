using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Runtime.DataSources;

/// <summary>
/// Alpaca Market Data (free IEX tier) data source — recommended default for real historical data.
/// Wraps the core library's <see cref="AlpacaClient.GetHistoricalBarsAsync"/> (officially maintained
/// Alpaca.Markets SDK, already a core dependency), unlike the Yahoo/Stooq sources which reverse-engineer
/// an undocumented endpoint. Requires a free API key from alpaca.markets (DataSourceKind.Alpaca).
/// </summary>
public sealed class AlpacaTraditionalFinanceSourceDataService : ITraditionalFinanceSourceDataService
{
    private readonly AlpacaClient _client;

    /// <summary>Builds the Alpaca data client from an API key/secret (paper credentials work for market data).</summary>
    /// <exception cref="ArgumentException">apiKey or apiSecret is blank.</exception>
    public AlpacaTraditionalFinanceSourceDataService(string apiKey, string apiSecret)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("apiKey must not be blank.", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(apiSecret))
            throw new ArgumentException("apiSecret must not be blank.", nameof(apiSecret));

        // Market data (IEX free tier) is identical between Paper and Live Alpaca accounts;
        // Paper avoids requiring live-trading approval just to read historical bars.
        _client = new AlpacaClient(new BrokerCredentials { ApiKey = apiKey, Secret = apiSecret }, ExchangeEnvironment.Paper);
    }

    /// <summary>Downloads daily OHLCV bars from Alpaca's free IEX feed.</summary>
    /// <exception cref="ArgumentException">symbol is blank.</exception>
    public async Task<Ohlcvs> DownloadOhlcvListAsync(
        string symbol, DateTime startDt, DateTime endDt,
        ResolutionLevel Period = ResolutionLevel.Daily,
        DataSource dataSource = DataSource.YahooFinance)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("symbol must not be blank.", nameof(symbol));

        var endUtc = DateTime.SpecifyKind(endDt, DateTimeKind.Utc);
        var startUtc = DateTime.SpecifyKind(startDt, DateTimeKind.Utc);
        // GetHistoricalBarsAsync is limit-based; over-request by calendar days (>= trading days) then
        // trim locally to [startUtc, endUtc] so the interface's date-range contract still holds.
        var calendarDays = Math.Max(1, (int)Math.Ceiling((endUtc - startUtc).TotalDays)) + 5;

        var underlying = new Underlying(symbol, AssetType.UsEquity);
        var bars = await _client.GetHistoricalBarsAsync(underlying, endUtc, calendarDays, Period)
            .ConfigureAwait(false);

        var set = new HashSet<Ohlcv>(bars.Where(b => b.OpenDateTime >= startUtc));
        var ordered = set.OrderBy(x => x.OpenDateTime).ToList();
        return new Ohlcvs
        {
            Symbol = symbol,
            ResolutionLevel = Period,
            StartDateTimeUtc = ordered.Count > 0 ? ordered[0].OpenDateTime : default,
            EndDateTimeUtc = ordered.Count > 0 ? ordered[^1].OpenDateTime : default,
            OhlcvSet = set
        };
    }

    /// <summary>Same download path as <see cref="DownloadOhlcvListAsync"/>.</summary>
    public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(
        string symbol, DateTime startDt, DateTime endDt,
        string fullPathFileName,
        ResolutionLevel Period = ResolutionLevel.Daily)
        => DownloadOhlcvListAsync(symbol, startDt, endDt, Period);

    /// <summary>Not applicable (online source).</summary>
    public Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename)
        => throw new NotSupportedException("Alpaca source does not support file-based reads.");

    /// <summary>Not applicable (online source).</summary>
    public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName)
        => throw new NotSupportedException("Alpaca source does not support file-based saves.");

    /// <summary>Not applicable (no equity-list endpoint wired here).</summary>
    public Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500)
        => throw new NotSupportedException("Alpaca source does not provide S&P 500 symbols.");
}
