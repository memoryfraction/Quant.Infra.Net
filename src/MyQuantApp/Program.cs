using Microsoft.Extensions.DependencyInjection;
using Python.Runtime;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Shared.Model;

class Program
{
    // =========================================================================
    // Python environment configuration
    // =========================================================================
    // Point these to your Anaconda / Miniconda virtual environment that has
    // the "yfinance" package installed.
    //
    // Setup steps (one-time):
    //   1. conda create -n quant python=3.9
    //   2. conda activate quant
    //   3. pip install yfinance
    //   4. Update the two constants below to match your environment:
    //      - CondaEnvPath : root folder of the conda env
    //                       e.g. "C:\Users\<you>\miniconda3\envs\quant"
    //                       or   "D:\ProgramData\PythonVirtualEnvs\pair_trading"
    //      - PythonDllName: the python DLL filename in that folder
    //                       e.g. "python39.dll" for Python 3.9
    // =========================================================================
    private const string CondaEnvPath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading";
    private const string PythonDllName = "python39.dll";

    static async Task Main(string[] args)
    {
        // 1. Register services
        var services = new ServiceCollection();
        services.AddScoped<IAnalysisService, AnalysisService>();
        var provider = services.BuildServiceProvider();

        var analysis = provider.GetRequiredService<IAnalysisService>();

        // 2. Download AAPL & MSFT 1-year daily close prices via Python yfinance
        var end = DateTime.UtcNow;
        var start = end.AddYears(-1);

        List<double> aaplClose;
        List<double> msftClose;
        bool usedSampleData = false;

        try
        {
            InitializePython();

            Console.WriteLine("Downloading AAPL daily OHLCV via yfinance...");
            aaplClose = DownloadCloseViaYFinance("AAPL", start, end);
            Console.WriteLine($"AAPL rows: {aaplClose.Count}");

            Console.WriteLine("Downloading MSFT daily OHLCV via yfinance...");
            msftClose = DownloadCloseViaYFinance("MSFT", start, end);
            Console.WriteLine($"MSFT rows: {msftClose.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"yfinance download failed: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Possible causes:");
            Console.WriteLine($"  1. Conda env not found at: {CondaEnvPath}");
            Console.WriteLine($"  2. Python DLL not found: {PythonDllName}");
            Console.WriteLine("  3. yfinance not installed (run: pip install yfinance)");
            Console.WriteLine();
            Console.WriteLine("Falling back to built-in sample data for demo...");
            Console.WriteLine();

            usedSampleData = true;

            // Sample AAPL & MSFT close prices (20 trading days)
            aaplClose = new List<double>
            {
                198.11, 197.57, 195.89, 196.94, 194.83,
                193.60, 195.18, 196.89, 198.23, 197.96,
                200.31, 202.64, 203.93, 205.28, 204.75,
                207.15, 209.07, 210.58, 211.75, 213.07
            };
            msftClose = new List<double>
            {
                374.51, 373.27, 370.73, 372.45, 369.14,
                367.75, 370.87, 373.56, 376.04, 375.28,
                380.55, 384.30, 386.73, 389.47, 388.27,
                392.66, 396.51, 399.12, 401.78, 404.22
            };

            Console.WriteLine($"AAPL sample rows: {aaplClose.Count}");
            Console.WriteLine($"MSFT sample rows: {msftClose.Count}");
        }

        // 3. Compute close-price correlation
        if (aaplClose.Count > 10 && msftClose.Count > 10)
        {
            if (usedSampleData)
                Console.WriteLine();

            Console.WriteLine("=== Analysis Results ===");

            // Align to same length
            int minLen = Math.Min(aaplClose.Count, msftClose.Count);
            aaplClose = aaplClose.Take(minLen).ToList();
            msftClose = msftClose.Take(minLen).ToList();

            double corr = analysis.CalculateCorrelation(aaplClose, msftClose);
            Console.WriteLine($"AAPL vs MSFT correlation: {corr:F4}");

            // 4. OLS regression
            var (slope, intercept) = analysis.PerformOLSRegression(aaplClose, msftClose);
            Console.WriteLine($"OLS regression: Slope={slope:F4}, Intercept={intercept:F4}");

            // 5. Compute spread and run ADF stationarity test
            var spread = msftClose
                .Zip(aaplClose, (m, a) => m - slope * a - intercept)
                .ToList();

            bool isStationary = analysis.AugmentedDickeyFullerTest(spread, adfTestStatisticThreshold: -2.86);
            Console.WriteLine($"Spread ADF stationary: {isStationary}");

            // 6. Latest Z-Score
            double zScore = analysis.CalculateZScores(spread, spread.Last());
            Console.WriteLine($"Latest Z-Score: {zScore:F4}");
        }
        else
        {
            Console.WriteLine("Insufficient data, skipping analysis.");
        }

        Console.WriteLine();
        Console.WriteLine("Done!");

        await Task.CompletedTask;
    }

    // =========================================================================
    // Python helpers (same pattern as AnalysisService.AugmentedDickeyFullerTestPython)
    // =========================================================================

    private static bool _pythonInitialized;
    private static readonly object _initLock = new();

    /// <summary>
    /// Initialize the pythonnet runtime (once per process).
    /// Uses PythonNetInfra from Quant.Infra.Net to resolve DLL / paths.
    /// </summary>
    private static void InitializePython()
    {
        if (_pythonInitialized) return;

        lock (_initLock)
        {
            if (_pythonInitialized) return;

            var infra = PythonNetInfra.GetPythonInfra(CondaEnvPath, PythonDllName);

            Runtime.PythonDLL = infra.PythonDLL;
            PythonEngine.PythonHome = infra.PythonHome;
            PythonEngine.PythonPath = infra.PythonPath;

            PythonEngine.Initialize();
            _pythonInitialized = true;
        }
    }

    /// <summary>
    /// Download daily close prices for a symbol using Python yfinance.
    /// Equivalent Python code:
    ///   import yfinance as yf
    ///   df = yf.download("AAPL", start="2024-01-01", end="2025-01-01")
    ///   close_list = df["Close"].values.flatten().tolist()
    /// </summary>
    private static List<double> DownloadCloseViaYFinance(string symbol, DateTime start, DateTime end)
    {
        using (Py.GIL())
        {
            dynamic yf = Py.Import("yfinance");

            string startStr = start.ToString("yyyy-MM-dd");
            string endStr = end.ToString("yyyy-MM-dd");

            // yf.download returns a pandas DataFrame.
            // In newer yfinance versions, columns are MultiIndex (e.g. ("Close","AAPL")),
            // so df["Close"] returns a DataFrame, not a Series.
            // Using .values.flatten().tolist() works for both cases.
            dynamic df = yf.download(symbol, start: startStr, end: endStr, auto_adjust: true);

            dynamic closeSeries = df.__getitem__("Close");
            dynamic pyList = closeSeries.values.flatten().tolist();

            var result = new List<double>();
            foreach (dynamic item in pyList)
            {
                result.Add((double)item);
            }
            return result;
        }
    }
}
