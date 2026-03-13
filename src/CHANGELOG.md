# Changelog / 变更日志

All notable changes to **Quant.Infra.Net** will be documented in this file.  
本文件记录 **Quant.Infra.Net** 的所有重要变更。

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),  
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

> **Versioning convention / 版本号约定**  
> `MAJOR.MINOR.PATCH`  
> - **MAJOR** – incompatible API changes / 不兼容的 API 变更  
> - **MINOR** – new features, backward-compatible / 新增功能（向后兼容）  
> - **PATCH** – bug fixes, backward-compatible / 缺陷修复（向后兼容）

---

## [1.2.3] - 2026-03-13

### Changed / 变更
- **Code standards enforcement across the entire project** – applied SOLID-aligned coding standards to 12+ core source files.  
  **全项目代码规范治理** ——对 12+ 个核心源文件实施了 SOLID 对齐的编码规范。
- Added bilingual (Chinese + English) XML documentation comments to all public methods, properties, and enum types in: `UtilityService`, `AnalysisService`, `Enums`, `RollingWindow`, `TimeSeries`, `Underlying`, `OLSRegressionData`, `Element`, `AdfTestResult`, `SpreadCalculatorRow`, `EmailService`, `CommercialEmailService`.  
  为上述文件中的所有公开方法、属性和枚举类型添加了中英文双语 XML 文档注释。
- Added parameter validation guards (`ArgumentNullException`, `ArgumentException`) at the beginning of all public methods in `UtilityService`, `AnalysisService`, `TimeSeries`, `Underlying`, `Element`.  
  在上述文件的所有公开方法入口处添加了参数有效性校验。
- Converted all Chinese console/log output to English in `CommercialEmailService` (20 messages) and `EmailService` to prevent garbled characters in non-CJK terminals.  
  将 `CommercialEmailService`（20 处）和 `EmailService` 中所有中文控制台/日志输出转换为英文，防止非中文终端乱码。

### Added / 新增
- `CHANGELOG.md` – project version history tracking file following [Keep a Changelog](https://keepachangelog.com) format, covering all versions from v0.0.1 to v1.2.3.  
  新增 `CHANGELOG.md` 版本变更记录文件，遵循 Keep a Changelog 格式，覆盖 v0.0.1 至 v1.2.3 的完整历史。

---

## [1.2.2] - 2026-02-12

### Added / 新增
- **Brevo commercial email service** (`CommercialEmailService`) with full SMTP integration for bulk email delivery.  
  新增 Brevo 商业邮件服务，支持批量 SMTP 邮件发送。
- `EmailServiceFactory` for automatic routing between `PersonalEmailService` and `CommercialEmailService` based on configuration.  
  新增邮件服务工厂，根据配置自动路由到个人或商业邮件服务。
- `PersonalEmailService` for single-recipient personal SMTP sending.  
  新增个人邮件服务，支持单收件人 SMTP 发送。
- Email model layer: `EmailMessage`, `EmailSettingBase`, `PersonalEmailSetting`, `CommercialEmailSetting`.  
  新增邮件模型层。

### Changed / 变更
- Downgraded AutoMapper to free version 12.0 to avoid licensing issues.  
  降级 AutoMapper 到免费版 12.0。
- Cleaned up unnecessary files from repository.  
  清理了仓库中的冗余文件。

---

## [1.2.1] - 2025-11-18

### Added / 新增
- `DownloadBinanceSpotIncrementalDataAsync()` and `DownloadBinancePerpetualContractIncrementalDataAsync()` for incremental data downloads, significantly reducing redundant API calls.  
  新增增量数据下载方法，显著减少重复数据拉取时间。
- `WriteCsvManually()` in `IOService` for manual CSV output control.  
  `IOService` 新增 `WriteCsvManually()` 方法。
- Python-based hourly backtesting support via `pythonnet` bridge.  
  支持基于 Python 的小时级回测。

### Fixed / 修复
- Resolved stationarity test (`AugmentedDickeyFullerTest`) returning incorrect results for certain edge cases.  
  修复了 ADF 平稳性检验在特定边界情况下结果不正确的问题。

---

## [1.2.0] - 2025-10-26

### Added / 新增
- **`ICryptoSourceDataService`** – new unified crypto data service interface.  
  新增统一的加密货币数据服务接口。
- `GetTopMarketCapSymbolsFromCoinMarketCapAsync()` – fetch top-N market-cap symbols from CoinMarketCap API.  
  新增从 CoinMarketCap 获取市值前 N 名的 Symbol 列表。
- `DownloadBinanceSpotAsync()` / `DownloadBinanceUsdFutureAsync()` – batch download Binance Spot and USD-M Futures OHLCV data to local CSV files.  
  新增批量下载 Binance 现货和 U 本位合约 K 线数据并存储为 CSV。
- `GetAllBinanceSpotSymbolsAsync()` / `GetAllBinanceUsdFutureSymbolsAsync()` – list all available trading symbols.  
  新增获取所有 Binance 现货/合约交易对。

### Changed / 变更
- Upgraded `pythonnet` to 3.0.5.  
  升级 `pythonnet` 至 3.0.5。
- Upgraded multiple NuGet packages to latest compatible versions.  
  升级了多个 NuGet 包到最新兼容版本。

---

## [1.1.2] - 2025-06-10

### Added / 新增
- **`USEquityAlpacaBrokerService`** – U.S. equity broker service powered by Alpaca Markets API, supporting:  
  新增美股经纪服务（基于 Alpaca Markets API），支持：
  - `SetHoldingsAsync()` – target-weight position management. / 目标权重持仓管理。
  - `LiquidateAsync()` – position liquidation. / 清仓操作。
  - `PlaceOrderAsync()` – order placement with Market/Limit/StopLoss types. / 下单（市价/限价/止损）。
  - `GetAccountEquityAsync()` – account equity query. / 账户市值查询。
  - `HasPositionAsync()` – position existence check. / 持仓检查。
  - `IsMarketOpeningAsync()` – market hours detection. / 开盘时间检测。
  - `GetFormattedAccountSummaryAsync()` – formatted account summary. / 格式化账户摘要。
- `AlpacaClient` with exponential backoff retry (1s, 4s, 9s) and response caching.  
  新增带指数退避重试和缓存的 Alpaca 客户端。
- `GetHistoricalBarsBatchAsync()` – batch historical data retrieval (1000 bars per request, 200ms interval).  
  新增批量历史数据获取方法。
- `IHistoricalDataSourceServiceTraditionalFinance` / `IRealtimeDataSourceServiceTraditionalFinance` interfaces.  
  新增传统金融历史/实时数据源接口。
- `ResolutionConversionService` – convert OHLCV data between resolution levels (e.g., Minute → Hourly).  
  新增分辨率转换服务。

### Fixed / 修复
- Fixed `IntervalTrigger` to guarantee no-miss, no-duplicate event firing using `_syncLock` and future-time calculation.  
  修复了 `IntervalTrigger` 不重不漏触发的问题。
- Fixed fractional share handling: non-integer quantities are now rounded to integers for short-selling compatibility.  
  修复了碎股处理问题，空头交易自动取整。
- Dollar-neutral position sizing replaces share-neutral sizing in pair trading.  
  配对交易改用 Dollar Neutral 而非 Share Neutral。

---

## [1.1.1] - 2025-05-23

### Added / 新增
- `StartMode.TodayBeforeUSMarketClose` – schedule tasks relative to U.S. market close time (16:00 ET) with configurable offset.  
  新增 `TodayBeforeUSMarketClose` 触发模式，支持相对美股收盘时间偏移。
- `GetEasternTimeZone()` utility method.  
  新增东部时区工具方法。
- Data resolution conversion with corresponding unit tests.  
  新增数据级别转换功能及单元测试。

---

## [1.1.0] - 2025-05-10

### Added / 新增
- `HistoricalDataSourceServiceMongodb` – MongoDB-backed historical data source service.  
  新增 MongoDB 历史数据源服务。
- MongoDB API integration for raw data retrieval.  
  集成 MongoDB API 获取原始数据。

---

## [1.0.6] - 2024-11-27

### Added / 新增
- `LICENSE` file (MIT License).  
  新增 MIT 许可证文件。
- Binance perpetual contract data download support (`DownloadBinancePerpetualContractOhlcvFiles`).  
  新增 Binance 永续合约数据下载支持。

### Fixed / 修复
- Fixed `IntervalTrigger.CalculateNextTriggerTime()` incorrectly adding delay on every restart instead of only the first time.  
  修复 `IntervalTrigger` 每次启动都叠加延迟的问题。
- Fixed trigger time disorder issues.  
  修复了触发时间错乱问题。

---

## [1.0.5] - 2024-10-16

### Added / 新增
- **`IBinanceUsdFutureService`** – dedicated Binance USD-M Futures service interface with:  
  新增 Binance U 本位合约服务接口，支持：
  - `GetusdFutureAccountBalanceAsync()` – account balance query. / 账户余额查询。
  - `GetusdFutureUnrealizedProfitRateAsync()` – unrealized PnL rate. / 未实现盈亏比率。
  - `SetUsdFutureHoldingsAsync()` – target-weight position management. / 目标权重持仓管理。
  - `LiquidateUsdFutureAsync()` – position liquidation. / 清仓操作。
  - `HasUsdFuturePositionAsync()` – position check. / 持仓检查。
  - `ShowPositionModeAsync()` / `SetPositionModeAsync()` – hedge/one-way mode management. / 双向/单向持仓模式管理。
- Binance Testnet support via `ExchangeEnvironment.Testnet`.  
  新增 Binance 测试网支持。
- Enhanced structured logging with `UtilityService.LogAndWriteLine()` – color-coded console + Serilog output.  
  增强的结构化日志：彩色控制台 + Serilog 输出。

### Fixed / 修复
- Fixed `SetUsdFutureHoldingsAsync()` to correctly handle both Long and Short positions when calculating holdings.  
  修复持仓计算未同时考虑多空双向持仓的问题。

---

## [1.0.4] - 2024-10-12

### Added / 新增
- `SpreadCalculatorFixLength` abstract class and `SpreadCalculatorPerpetualContract` / `SpreadCalculatorUSEquity` implementations for pair trading spread calculation using fixed-window extrapolation.  
  新增定长窗口外推法残差计算器抽象类及永续合约/美股实现。
- `UpsertRow()` – real-time row update for streaming spread calculation.  
  新增实时行情插入/更新方法。
- `UpsertSpreadAndEquation()` – auto-compute Spread, Equation, HalfLife columns.  
  新增自动计算 Spread、Equation、HalfLife 列。
- `GetSpreadsFromColumn()` – extract spread values from DataFrame column.  
  新增从 DataFrame 列提取残差序列。
- `IntervalTrigger` – configurable periodic event trigger supporting NextSecond/NextMinute/NextHour/NextDay modes.  
  新增定时触发器，支持多种触发模式。
- `InMemoryBinanceBrokerService` – in-memory broker service for backtesting.  
  新增内存模拟经纪服务用于回测。

---

## [1.0.3] - 2024-09-09

### Added / 新增
- **Portfolio management system**:  
  新增投资组合管理系统：
  - `PortfolioBase` abstract class with `StockPortfolio` and `CryptoPortfolio` concrete implementations.  
    `PortfolioBase` 抽象基类，及股票/加密货币组合实现。
  - `Balance` model – tracks NetLiquidationValue, MarketValue, Cash, UnrealizedPnL.  
    `Balance` 模型——跟踪净清算价值、市值、现金、未实现盈亏。
  - `Position` / `Positions` models – position tracking with cost-basis and unrealized PnL calculation.  
    `Position` 模型——持仓跟踪、成本基和未实现盈亏计算。
  - `PortfolioCalculationService` – static methods for `CalculateBalance()` and `CalculatePositions()`.  
    `PortfolioCalculationService`——余额和持仓静态计算方法。
  - `StrategyPerformanceAnalyzer` – CAGR, Sharpe Ratio, Calmar Ratio, Maximum Drawdown.  
    `StrategyPerformanceAnalyzer`——年化收益率、夏普比率、卡尔玛比率、最大回撤。
  - `DrawChart()` – portfolio market value visualization via ScottPlot.  
    `DrawChart()`——基于 ScottPlot 的组合市值可视化。
- `OrderBase` model with support for Market, Limit, StopLoss, TakeProfit, and other order types.  
  新增 `OrderBase` 订单模型，支持市价/限价/止损/止盈等订单类型。

---

## [1.0.2] - 2024-08-16

### Added / 新增
- **`AugmentedDickeyFullerTestPython()`** – ADF test via Python `statsmodels` bridge for production-grade p-value accuracy.  
  新增通过 Python `statsmodels` 桥接的 ADF 检验，获取生产级精度的 p 值。
- `AugmentedDickeyFullerTest()` – pure C# ADF implementation using OLS regression with MacKinnon p-value approximation.  
  新增纯 C# ADF 检验实现。
- `PythonNetInfra` / `PythonInitializer` – Python.NET environment configuration and initialization.  
  新增 Python.NET 环境配置与初始化。
- `PerformOLSRegression()` – OLS linear regression returning (Slope, Intercept).  
  新增 OLS 最小二乘法回归。
- `CalculateZScores()` – Z-Score calculation (3 overloads).  
  新增 Z-Score 计算（3 个重载）。
- `CalculateHalfLife()` – mean-reversion half-life calculation from spread series.  
  新增均值回归半衰期计算。
- `LoadCsvToDataFrame()` – CSV to `Microsoft.Data.Analysis.DataFrame` loader.  
  新增 CSV 转 DataFrame 加载器。

### Changed / 变更
- Renamed regression method to `PerformOLSRegression()` for clarity.  
  重命名回归方法以提高清晰度。

---

## [1.0.1] - 2024-07-28

### Added / 新增
- `CalculateCorrelation()` – Pearson correlation coefficient calculation.  
  新增 Pearson 相关性系数计算。
- `PerformShapiroWilkTest()` – Shapiro-Wilk normal distribution test.  
  新增 Shapiro-Wilk 正态分布检验。
- `GetSp500SymbolsAsync()` – scrape S&P 500 constituents from Wikipedia.  
  新增从 Wikipedia 抓取标普 500 成分股列表。
- `SaveOhlcvsToCsv()` – save Binance OHLCV data to CSV.  
  新增 Binance K 线数据保存到 CSV。
- Dependency Injection (DI) support with `Microsoft.Extensions.DependencyInjection`.  
  新增依赖注入支持。

---

## [1.0.0] - 2024-07-06

### Added / 新增
- **InteractiveBrokers (IBKR) integration** via InterReact project reference:  
  新增盈透证券集成（通过 InterReact 项目引用）：
  - `IBKRService` / `IIBKRService` – IBKR order management interface.  
    IBKR 订单管理接口。
  - `InteractiveBrokersService` – broker service implementation.  
    盈透证券经纪服务实现。
  - `OrderBinanceSpot` / `OrderIBKR` – exchange-specific order models.  
    交易所特定订单模型。
  - `OrderFactory` – factory pattern for creating exchange-appropriate order objects.  
    工厂模式创建交易所对应的订单对象。

### Changed / 变更
- Upgraded to **.NET 6** (from .NET 7) for LTS compatibility.  
  升级到 .NET 6 LTS 版本。

---

## [0.2.0] - 2024-05-31

### Added / 新增
- `PlaceMarketOrderAsync()` for IBKR market order execution.  
  新增 IBKR 市价单执行方法。
- InterReact project reference for Interactive Brokers connectivity.  
  添加 InterReact 项目引用以连接盈透证券。

### Changed / 变更
- Upgraded YahooFinanceApi to 2.3.3.  
  升级 YahooFinanceApi 至 2.3.3。

---

## [0.1.0] - 2024-01-23

### Added / 新增
- `BinanceOrderService` – Binance order management:  
  新增 Binance 订单服务：
  - `GetAllOpenOrderAsync()` – query all open orders. / 查询所有未成交订单。
  - `GetAllSymbolsAsync()` – list all trading symbols. / 获取所有交易对。
  - `CancelAllOrdersAsync()` – cancel all open orders. / 取消所有订单。
  - `SetBinanceCredential()` – runtime API key configuration. / 运行时 API 密钥配置。

### Changed / 变更
- Binance services now accept credentials as input parameters instead of reading from configuration, improving library reusability.  
  Binance 服务改为接受入参凭证，提高了库的可复用性。

---

## [0.0.2] - 2024-01-17

### Added / 新增
- **DingTalk notification service** (`DingtalkService`) with HMAC-SHA256 signature support.  
  新增钉钉机器人通知服务（含 HMAC-SHA256 签名）。
- **WeChat Work notification service** (`WeChatService`) for group chat text messages.  
  新增企业微信群聊文本通知服务。
- `ITraditionalFinanceSourceDataService` – Yahoo Finance data download interface.  
  新增 Yahoo Finance 数据下载接口。
  - `DownloadOhlcvListAsync()` – download OHLCV data. / 下载 K 线数据。
  - `BeginSyncSourceDailyDataAsync()` – sync and persist data to CSV. / 同步并持久化数据。
- User Secrets support for secure credential management.  
  新增 User Secrets 支持，安全管理凭证。

---

## [0.0.1] - 2024-01-15

### Added / 新增
- Initial project creation targeting **.NET 8**.  
  初始项目创建，目标框架 .NET 8。
- Solution structure: `Quant.Infra.Net` (library) + `Quant.Infra.Net.Tests` (unit tests).  
  解决方案结构：类库 + 单元测试。
- Core OHLCV data models (`BasicOhlcv`, `Ohlcv`, `Ohlcvs`).  
  核心 OHLCV 数据模型。
- Shared enumerations: `Broker`, `AssetType`, `ResolutionLevel`, `OrderStatus`, `Currency`, etc.  
  共享枚举定义。
- `RollingWindow<T>` – generic fixed-size sliding window data structure.  
  泛型定长滑动窗口数据结构。
- Yahoo Finance API integration for historical market data.  
  集成 Yahoo Finance API 获取历史行情。

---

<!-- 
============================================================
  HOW TO MAINTAIN THIS FILE / 如何维护此文件
============================================================

1. When releasing a new version, add a new section at the TOP 
   (below the header), using the format:
   发布新版本时，在顶部添加新章节，格式如下：

   ## [x.y.z] - YYYY-MM-DD
   ### Added / 新增
   ### Changed / 变更
   ### Deprecated / 废弃
   ### Removed / 移除
   ### Fixed / 修复
   ### Security / 安全

2. Update the <Version> in Quant.Infra.Net.csproj to match.
   同步更新 .csproj 中的 <Version> 版本号。

3. Optionally create a Git tag:
   可选：创建 Git 标签：
   git tag -a v1.2.2 -m "Release v1.2.2"
   git push origin v1.2.2

4. Categories explained / 分类说明:
   - Added:      New features / 新功能
   - Changed:    Changes to existing features / 已有功能的变更
   - Deprecated: Features to be removed soon / 即将移除的功能
   - Removed:    Removed features / 已移除的功能
   - Fixed:      Bug fixes / 缺陷修复
   - Security:   Vulnerability fixes / 安全漏洞修复
-->
