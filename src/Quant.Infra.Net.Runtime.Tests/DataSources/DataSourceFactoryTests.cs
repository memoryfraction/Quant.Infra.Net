using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.SourceData.Service.Historical;
using Quant.Infra.Net.Runtime.DataSources;
using Quant.Infra.Net.Runtime.Models;

namespace Quant.Infra.Net.Runtime.Tests.DataSources;

/// <summary>
/// R2 验收测试：4 个内置 Kind 分别解析到正确类型；Custom 缺实例 → ArgumentException（fail-fast）。
/// R2 acceptance tests: the 4 built-in kinds each resolve to the correct type; Custom without an instance throws ArgumentException.
/// </summary>
[TestClass]
public class DataSourceFactoryTests
{
    private const string FakeSymbol = "FAKE";

    /// <summary>R2 验收 ①：内置 4 Kind（Demo/Yahoo/Csv/Binance）分别解析到正确类型 / The 4 built-in kinds each resolve to their expected type.</summary>
    [TestMethod]
    public void BuiltIn_Kinds_Resolve_To_Expected_Types()
    {
        var provider = Provider();

        Assert.IsInstanceOfType(DataSourceFactory.Create(DataSourceKind.Demo, provider, null), typeof(DemoSyntheticSourceDataService));
        Assert.IsInstanceOfType(DataSourceFactory.Create(DataSourceKind.Yahoo, provider, null), typeof(TraditionalFinanceSourceDataService));
        Assert.IsInstanceOfType(DataSourceFactory.Create(DataSourceKind.Csv, provider, null), typeof(TraditionalFinanceSourceDataService));
        Assert.IsInstanceOfType(DataSourceFactory.Create(DataSourceKind.Binance, provider, null), typeof(BinanceKlineSourceDataService));
    }

    /// <summary>Demo Kind 必须返回确定性非空序列（零网络）/ Demo kind yields a deterministic non-empty series (zero network).</summary>
    [TestMethod]
    public void Demo_Kind_Returns_Deterministic_Series()
    {
        var source = DataSourceFactory.Create(DataSourceKind.Demo, Provider(), null);
        var ohlcvs = source.DownloadOhlcvListAsync(FakeSymbol, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow).GetAwaiter().GetResult();

        Assert.AreEqual(FakeSymbol, ohlcvs.Symbol);
        Assert.IsTrue(ohlcvs.OhlcvSet.Count > 0);
    }

    /// <summary>Binance Kind 必须返回 K 线适配且转发到 broker.GetOhlcvListAsync（只读）/ Binance kind returns the kline adapter forwarding to the broker.</summary>
    [TestMethod]
    public void Binance_Kind_Forwards_To_Broker()
    {
        var fake = new FakeBinanceBroker();
        var sp = new ServiceCollection()
            .AddSingleton<IBinanceUsdFutureService>(fake)
            .BuildServiceProvider();

        var source = DataSourceFactory.Create(DataSourceKind.Binance, sp, null);
        var ohlcvs = source.DownloadOhlcvListAsync(FakeSymbol, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow).GetAwaiter().GetResult();

        Assert.AreEqual(1, fake.CallCount);
        Assert.AreEqual(FakeSymbol, ohlcvs.Symbol);
    }

    /// <summary>Yahoo Kind 未注册 IHistoricalDataSourceService → 容器 fail-fast / Yahoo kind without IHistoricalDataSourceService fails fast from the container.</summary>
    [TestMethod]
    public void Yahoo_Kind_Requires_Historical_Service()
        => Assert.ThrowsException<InvalidOperationException>(
            () => DataSourceFactory.Create(DataSourceKind.Yahoo, new ServiceCollection().BuildServiceProvider(), null));

    /// <summary>R2 验收 ②：Custom 缺 customDataSource → ArgumentException（fail-fast，绝不静默回退）/ Custom without customDataSource throws ArgumentException.</summary>
    [TestMethod]
    public void Custom_Without_Instance_Throws_ArgumentException()
        => Assert.ThrowsException<ArgumentException>(
            () => DataSourceFactory.Create(DataSourceKind.Custom, Provider(), null));

    /// <summary>Custom 提供实例 → 原样返回；Demo Kind 忽略 customDataSource / Custom returns the instance as-is; Demo kind ignores it.</summary>
    [TestMethod]
    public void Custom_With_Instance_Returns_It_And_Demo_Ignores_It()
    {
        var custom = new DemoSyntheticSourceDataService();

        Assert.AreSame(custom, DataSourceFactory.Create(DataSourceKind.Custom, Provider(), custom));
        Assert.AreNotSame(custom, DataSourceFactory.Create(DataSourceKind.Demo, Provider(), custom));
    }

    /// <summary>参数校验：null provider → ArgumentNullException（§11.8）/ Parameter validation: null provider → ArgumentNullException.</summary>
    [TestMethod]
    public void Null_ServiceProvider_Throws()
        => Assert.ThrowsException<ArgumentNullException>(
            () => DataSourceFactory.Create(DataSourceKind.Demo, null!, null));

    private static IServiceProvider Provider()
        => new ServiceCollection()
            .AddSingleton<IHistoricalDataSourceService, HistoricalDataSourceServiceCsv>()
            .AddSingleton<IBinanceUsdFutureService>(new PaperBinanceUsdFutureService(null))
            .BuildServiceProvider();
}

/// <summary>
/// 测试 fake broker：记录 K 线调用次数并返回固定单根 K 线；其余成员不支持（本测试只驱动 K 线路径）。
/// Test fake broker: records kline calls and returns one fixed candle; other members unsupported (only the kline path is driven).
/// </summary>
public sealed class FakeBinanceBroker : IBinanceUsdFutureService
{
    private int _callCount;

    /// <summary>K 线调用次数 / Kline call count.</summary>
    public int CallCount => _callCount;

    /// <summary>K 线路径（转发目标）/ Kline path (the forwarded-to member).</summary>
    public Task<Ohlcvs> GetOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel)
    {
        Interlocked.Increment(ref _callCount);
        var ohlcv = new Ohlcv { Symbol = symbol, OpenDateTime = startDt, CloseDateTime = endDt, Open = 1m, High = 1m, Low = 1m, Close = 1m, Volume = 1m };
        return Task.FromResult(new Ohlcvs { Symbol = symbol, OhlcvSet = new HashSet<Ohlcv> { ohlcv } });
    }

    /// <summary>不支持（fake 不驱动）/ Not supported by the fake.</summary>
    public Task<IEnumerable<string>> GetUsdFutureSymbolsAsync() => throw new NotSupportedException();

    /// <summary>不支持（fake 不驱动）/ Not supported by the fake.</summary>
    public Task<IEnumerable<BinancePositionDetailsUsdt>> GetHoldingPositionAsync() => throw new NotSupportedException();

    /// <summary>环境属性（fake 占位）/ Environment property (fake placeholder).</summary>
    public ExchangeEnvironment ExchangeEnvironment { get; set; } = ExchangeEnvironment.Paper;

    /// <summary>不支持（fake 不驱动）/ Not supported by the fake.</summary>
    public Task<decimal> GetusdFutureAccountBalanceAsync() => throw new NotSupportedException();

    /// <summary>不支持（fake 不驱动）/ Not supported by the fake.</summary>
    public Task<double> GetusdFutureUnrealizedProfitRateAsync() => throw new NotSupportedException();

    /// <summary>不支持（fake 不驱动）/ Not supported by the fake.</summary>
    public Task LiquidateUsdFutureAsync(string symbol) => throw new NotSupportedException();

    /// <summary>不支持（fake 不驱动）/ Not supported by the fake.</summary>
    public Task SetUsdFutureHoldingsAsync(string symbol, double rate, PositionSide positionSide = PositionSide.Both) => throw new NotSupportedException();

    /// <summary>不支持（fake 不驱动）/ Not supported by the fake.</summary>
    public Task<bool> HasUsdFuturePositionAsync(string symbol) => throw new NotSupportedException();

    /// <summary>不支持（fake 不驱动）/ Not supported by the fake.</summary>
    public Task ShowPositionModeAsync() => throw new NotSupportedException();

    /// <summary>不支持（fake 不驱动）/ Not supported by the fake.</summary>
    public Task SetPositionModeAsync(bool isHedgeMode = true) => throw new NotSupportedException();
}
