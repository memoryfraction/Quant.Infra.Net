using System.Globalization;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Runtime.DataSources;

/// <summary>
/// Stooq free daily-bar data source (Stooq kind).
/// Downloads daily CSV from stooq.com/q/d/l/?s={symbol}.us&amp;i=d
/// No API key required; community source, no SLA guarantee (design section 7.7).
/// Daily bars only; Download path only. Network/format failures produce clear messages.
/// </summary>
public sealed class StooqTraditionalFinanceSourceDataService : ITraditionalFinanceSourceDataService
{
    private static readonly CultureInfo _invariant = CultureInfo.InvariantCulture;
    private const string DefaultUserAgent = "QuantInfraNet/1.0 (research use)";
    private readonly HttpClient _http;

    /// <summary>Uses a default HttpClient with a 30s timeout and explicit User-Agent.</summary>
    public StooqTraditionalFinanceSourceDataService() : this(CreateDefaultClient()) { }

    /// <summary>Uses a caller-supplied HttpClient (tests inject a fake handler).</summary>
    public StooqTraditionalFinanceSourceDataService(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <summary>Downloads daily OHLCV from Stooq.</summary>
    /// <exception cref="ArgumentException">symbol is blank.</exception>
    /// <exception cref="InvalidOperationException">Network failure, non-200 response, or malformed CSV.</exception>
    public async Task<Ohlcvs> DownloadOhlcvListAsync(
        string symbol, DateTime startDt, DateTime endDt,
        ResolutionLevel Period = ResolutionLevel.Daily,
        DataSource dataSource = DataSource.YahooFinance)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("symbol must not be blank.", nameof(symbol));

        var url = BuildUrl(symbol);
        EnsureUserAgent();
        string csv;
        try
        {
            using var response = await _http
                .GetAsync(url, HttpCompletionOption.ResponseContentRead)
                .ConfigureAwait(false);
            csv = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    "Stooq request failed for " + symbol +
                    ": HTTP " + (int)response.StatusCode + " " + response.StatusCode +
                    " for " + url +
                    ". Please check the symbol is a valid US equity ticker on stooq.com.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Stooq request failed for " + symbol + " at " + url + ": " + ex.Message, ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException(
                "Stooq request timed out for " + symbol + " at " + url + ".", ex);
        }

        try
        {
            return ParseCsv(symbol, csv);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Stooq CSV is malformed for " + symbol +
                " (response from " + url + "): " + ex.Message, ex);
        }
    }

    /// <summary>Same daily CSV path as Download.</summary>
    public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(
        string symbol, DateTime startDt, DateTime endDt,
        string fullPathFileName,
        ResolutionLevel Period = ResolutionLevel.Daily)
        => DownloadOhlcvListAsync(symbol, startDt, endDt, Period);

    /// <summary>Not applicable (online source).</summary>
    public Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename)
        => throw new NotSupportedException("Stooq source does not support file-based reads.");

    /// <summary>Not applicable (online source).</summary>
    public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName)
        => throw new NotSupportedException("Stooq source does not support file-based saves.");

    /// <summary>Not applicable (no equity list endpoint).</summary>
    public Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500)
        => throw new NotSupportedException("Stooq source does not provide S&P 500 symbols.");

    /// <summary>Builds the Stooq daily CSV URL (symbol lower-cased, .us suffix).</summary>
    internal static string BuildUrl(string symbol)
        => "https://stooq.com/q/d/l/?s=" + symbol.Trim().ToLowerInvariant() + ".us&i=d";

    /// <summary>Parses Stooq CSV text into an Ohlcvs instance.</summary>
    /// <param name="symbol">Ticker symbol.</param>
    /// <param name="csv">CSV text (header optional); zero data rows yield an empty OhlcvSet.</param>
    internal static Ohlcvs ParseCsv(string symbol, string csv)
    {
        var set = new HashSet<Ohlcv>();
        if (string.IsNullOrWhiteSpace(csv))
            return new Ohlcvs
            {
                Symbol = symbol,
                ResolutionLevel = ResolutionLevel.Daily,
                OhlcvSet = set
            };

        var assembly = GetCsvHelperAssembly();
        var configType = assembly.GetType("CsvHelper.Configuration.CsvConfiguration")
            ?? assembly.GetType("CsvHelper.CsvConfiguration")
            ?? throw new InvalidOperationException("CsvHelper.CsvConfiguration type not found.");
        var config = Activator.CreateInstance(configType, _invariant)
            ?? throw new InvalidOperationException("Failed to create CsvHelper.CsvConfiguration instance.");
        SetHasHeaderRecord(config, true);

        using var reader = new StringReader(csv);
        var csvReader = CreateCsvReader(reader, config);
        foreach (var row in (System.Collections.IEnumerable)GetRecords(csvReader))
        {
            var rowObj = (dynamic)row;
            var volume = (decimal)rowObj.Volume;
            if (volume < 0) continue;
            set.Add(new Ohlcv
            {
                Symbol = symbol,
                OpenDateTime = (DateTime)rowObj.Date,
                CloseDateTime = ((DateTime)rowObj.Date).Add(TimeSpan.FromDays(1)),
                Open = (decimal)rowObj.Open,
                High = (decimal)rowObj.High,
                Low = (decimal)rowObj.Low,
                Close = (decimal)rowObj.Close,
                Volume = volume
            });
        }

        var ordered = set.OrderBy(x => x.OpenDateTime).ToList();
        return new Ohlcvs
        {
            Symbol = symbol,
            ResolutionLevel = ResolutionLevel.Daily,
            StartDateTimeUtc = ordered.Count > 0 ? ordered[0].OpenDateTime : default,
            EndDateTimeUtc = ordered.Count > 0 ? ordered[^1].OpenDateTime : default,
            OhlcvSet = set
        };
    }

    /// <summary>Gets the CsvHelper assembly (transitive dependency of the core library).</summary>
    private static System.Reflection.Assembly GetCsvHelperAssembly()
    {
        // 1. Already loaded?
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "CsvHelper");
        if (asm is not null) return asm;

        // 2. Try to load by name from the current directory (test/runtime output dir).
        try
        {
            asm = System.Runtime.Loader.AssemblyLoadContext.Default
                .LoadFromAssemblyName(new System.Reflection.AssemblyName("CsvHelper"));
            if (asm is not null) return asm;
        }
        catch { /* fall through to path-based loading */ }

        // 3. Load from the application's base directory (where the test/host DLL lives).
        var baseDir = System.AppContext.BaseDirectory;
        var dllPath = System.IO.Path.Combine(baseDir, "CsvHelper.dll");
        if (System.IO.File.Exists(dllPath))
            return System.Reflection.Assembly.LoadFrom(dllPath);

        throw new InvalidOperationException(
            "CsvHelper assembly not found. Expected in " + baseDir +
            " or loadable by name. Add CsvHelper to the output directory.");
    }

    /// <summary>Creates a CsvHelper.CsvReader via reflection.</summary>
    private static object CreateCsvReader(TextReader reader, object config)
    {
        var readerType = GetCsvHelperAssembly().GetType("CsvHelper.CsvReader")
            ?? throw new InvalidOperationException("CsvHelper.CsvReader type not found.");

        // Use the (TextReader, IReaderConfiguration, bool) constructor.
        var ctor = readerType.GetConstructors()
            .FirstOrDefault(ct =>
            {
                var ps = ct.GetParameters();
                return ps.Length == 3
                    && ps[0].ParameterType == typeof(TextReader)
                    && ps[2].ParameterType == typeof(bool)
                    && !ps[1].ParameterType.IsPrimitive
                    && ps[1].Name != "culture";
            })
            ?? readerType.GetConstructors()
            .FirstOrDefault(ct =>
            {
                var ps = ct.GetParameters();
                return ps.Length == 3
                    && ps[0].ParameterType == typeof(TextReader)
                    && ps[2].ParameterType == typeof(bool);
            })
            ?? throw new InvalidOperationException(
                "CsvHelper.CsvReader 3-param constructor not found.");

        return ctor.Invoke(new object?[] { reader, config, false });
    }

    /// <summary>Gets records from a CsvHelper.CsvReader via reflection.</summary>
    private static object GetRecords(object csvReader)
    {
        var method = csvReader.GetType().GetMethods()
            .First(m => m.Name == "GetRecords" && m.IsGenericMethodDefinition);
        var genericMethod = method.MakeGenericMethod(typeof(StooqCsvRow));
        var result = genericMethod.Invoke(csvReader, null)
            ?? throw new InvalidOperationException("CsvHelper.GetRecords returned null.");
        return result;
    }

    /// <summary>Sets HasHeaderRecord on a CsvConfiguration instance via reflection.</summary>
    private static void SetHasHeaderRecord(object config, bool value)
    {
        var prop = config.GetType().GetProperty("HasHeaderRecord");
        if (prop is not null) prop.SetValue(config, value);
    }

    /// <summary>Ensures the User-Agent header is set (some public APIs reject the .NET default UA).</summary>
    private void EnsureUserAgent()
    {
        if (!_http.DefaultRequestHeaders.TryGetValues("User-Agent", out _))
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
    }

    /// <summary>Creates a default HttpClient with a 30s timeout and explicit User-Agent.</summary>
    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient(new HttpClientHandler())
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        return client;
    }

    /// <summary>CsvHelper mapping carrier for one Stooq CSV row (column names match the endpoint).</summary>
    internal sealed class StooqCsvRow
    {
        /// <summary>Trade date.</summary>
        public DateTime Date { get; set; }

        /// <summary>Open price.</summary>
        public decimal Open { get; set; }

        /// <summary>High price.</summary>
        public decimal High { get; set; }

        /// <summary>Low price.</summary>
        public decimal Low { get; set; }

        /// <summary>Close price.</summary>
        public decimal Close { get; set; }

        /// <summary>Volume.</summary>
        public decimal Volume { get; set; }
    }
}