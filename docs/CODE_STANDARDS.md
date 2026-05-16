# Code Standards for Quant.Infra.Net

This document defines the mandatory coding standards for the Quant.Infra.Net project. All contributors must adhere to these guidelines to ensure code quality, maintainability, and consistency.

---

## Table of Contents

1. [SOLID Principles](#solid-principles)
2. [XML Documentation Standards](#xml-documentation-standards)
3. [Parameter Validation](#parameter-validation)
4. [Coding Language Standards](#coding-language-standards)
5. [Time Handling Standards](#time-handling-standards)
6. [Enum Management](#enum-management)
7. [README Maintenance](#readme-maintenance)
8. [Sensitive Data Protection](#sensitive-data-protection)
9. [Code Review Checklist](#code-review-checklist)

---

## SOLID Principles

All code must comply with SOLID design principles:

### 1. Single Responsibility Principle (SRP)
- Each class should have only one reason to change
- Methods should perform a single, well-defined task
- Example: `AnalysisService` handles statistical analysis only, not data fetching

### 2. Open/Closed Principle (OCP)
- Classes should be open for extension but closed for modification
- Use interfaces and abstract classes to enable extensibility
- Example: `IBrokerService` interface allows adding new brokers without modifying existing code

### 3. Liskov Substitution Principle (LSP)
- Derived classes must be substitutable for their base classes
- Do not violate base class contracts in derived implementations
- Example: All broker implementations must honor the `ExchangeEnvironment` property contract

### 4. Interface Segregation Principle (ISP)
- Clients should not be forced to depend on interfaces they do not use
- Prefer multiple small, specific interfaces over large, general-purpose ones
- Example: Separate `IHistoricalDataSourceService` and `IRealtimeDataSourceService` instead of one monolithic interface

### 5. Dependency Inversion Principle (DIP)
- High-level modules should not depend on low-level modules
- Depend on abstractions, not concretions
- Example: Services depend on `IConfiguration` abstraction, not concrete configuration implementations

---

## XML Documentation Standards

### Rule: All public members must have bilingual (Chinese + English) XML documentation

#### Format Template

```csharp
/// <summary>
/// 方法的中文描述。
/// English description of the method.
/// </summary>
/// <param name="paramName">参数的中文说明 / English parameter description.</param>
/// <returns>返回值的中文说明 / English return value description.</returns>
/// <exception cref="ArgumentException">当参数无效时抛出 / Thrown when parameter is invalid.</exception>
```

#### Examples

**Good:**
```csharp
/// <summary>
/// 计算两个时间序列的 Pearson 相关性。
/// Calculates the Pearson correlation between two time series.
/// </summary>
/// <param name="seriesA">时间序列A / Time series A.</param>
/// <param name="seriesB">时间序列B / Time series B.</param>
/// <returns>相关性系数 / The correlation coefficient.</returns>
/// <exception cref="ArgumentNullException">当参数为 null 时抛出 / Thrown when parameters are null.</exception>
public double CalculateCorrelation(IEnumerable<double> seriesA, IEnumerable<double> seriesB)
```

**Bad:**
```csharp
// Missing XML documentation
public double CalculateCorrelation(IEnumerable<double> seriesA, IEnumerable<double> seriesB)
```

#### Requirements
- Every `public` class, interface, method, property, and field must have XML documentation
- Both Chinese and English descriptions are mandatory
- Use consistent formatting: Chinese first, then English separated by "/"
- Document all parameters, return values, and exceptions

---

## Parameter Validation

### Rule: All public methods must validate parameters at the beginning

#### Validation Patterns

**Null Checks:**
```csharp
if (param == null) 
    throw new ArgumentNullException(nameof(param));
```

**String Validation:**
```csharp
if (string.IsNullOrWhiteSpace(param)) 
    throw new ArgumentException("Parameter must not be null or empty.", nameof(param));
```

**Range Validation:**
```csharp
if (param <= 0) 
    throw new ArgumentOutOfRangeException(nameof(param), "Parameter must be positive.");
```

**Date Validation:**
```csharp
if (startDt > endDt) 
    throw new ArgumentException("Start date must be earlier than or equal to end date.", nameof(startDt));
```

#### Complete Example

```csharp
public async Task<List<Ohlcv>> GetOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt)
{
    // Parameter validation - MUST be at the beginning
    if (string.IsNullOrWhiteSpace(symbol)) 
        throw new ArgumentException("Symbol must not be null or empty.", nameof(symbol));
    if (startDt > endDt) 
        throw new ArgumentException("Start date must be earlier than or equal to end date.", nameof(startDt));
    
    // Business logic follows...
}
```

#### Error Message Standards
- All error messages must be in **English** to prevent encoding issues
- Include the parameter name using `nameof()` operator
- Be specific about what validation failed

---

## Coding Language Standards

### Rule: All runtime output must be in English

#### Scope
- `Console.WriteLine()` messages
- Log messages (`Log.Information()`, `Log.Error()`, etc.)
- Exception messages
- User-facing strings

#### Examples

**Good:**
```csharp
Console.WriteLine($"Window is full, total {window.Count} elements");
Log.Error("Download failed, please check network connection");
throw new InvalidOperationException("CoinMarketCap: Response missing status field.");
```

**Bad:**
```csharp
Console.WriteLine($"窗口已满，共 {window.Count} 个元素");
Log.Error("下载失败，请检查网络连接");
throw new InvalidOperationException("CoinMarketCap: 响应缺少 status 字段。");
```

#### Rationale
- Prevents character encoding issues (乱码)
- Ensures compatibility across different systems and locales
- Facilitates international collaboration

#### Exception
- **Code comments** should remain bilingual (Chinese + English) for better understanding by Chinese developers

---

## Time Handling Standards

### Rule: All database operations and persistence must use UTC time

#### Guidelines

**Use `DateTime.UtcNow`:**
```csharp
var timestamp = DateTime.UtcNow;  // ✅ Correct
var record = new Record { CreatedAt = DateTime.UtcNow };
```

**Avoid `DateTime.Now`:**
```csharp
var timestamp = DateTime.Now;  // ❌ Wrong - uses local time
```

**Unix Timestamp Calculation:**
```csharp
// Correct way to calculate Unix timestamp
var timestamp = ((DateTime.UtcNow.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks) / 10000).ToString();
```

#### Affected Areas
- Database timestamps
- File modification times
- API request/response timestamps
- Log timestamps
- Cache expiry times

#### Rationale
- Ensures consistency across different time zones
- Prevents daylight saving time issues
- Simplifies time comparison and sorting

---

## Enum Management

### Rule: All enums must be centralized in `Shared/Model/Enums.cs`

#### Requirements

1. **Centralization**: No enums should be defined outside `Shared/Model/Enums.cs`
2. **Documentation**: Every enum and enum value must have bilingual XML documentation
3. **Naming**: Use PascalCase for enum names and values
4. **Explicit Values**: Always specify explicit integer values for enum members

#### Example

```csharp
/// <summary>
/// 交易所环境：测试网、实盘、模拟盘。
/// Exchange environment: testnet, live, or paper trading.
/// </summary>
public enum ExchangeEnvironment
{
    /// <summary>
    /// 测试网环境，用于开发和测试 / Testnet environment for development and testing.
    /// </summary>
    Testnet = 0,
    
    /// <summary>
    /// 实盘环境，使用真实资金进行交易 / Live environment with real funds for trading.
    /// </summary>
    Live = 1,
    
    /// <summary>
    /// 模拟盘环境，使用虚拟资金模拟实盘 / Paper trading environment with virtual funds simulating live trading.
    /// </summary>
    Paper = 2
}
```

#### Current Enums
- `ExchangeEnvironment` - Trading environment types
- `StartMode` - Timer trigger modes
- `MarketType` - Market classifications
- `Broker` - Supported brokers
- `OrderStatus` - Order lifecycle states
- `AssetType` - Asset classifications
- `ResolutionLevel` - Time series resolutions
- `DataSource` - Data source providers
- `TradeDirection` - Long/Short positions
- `Currency` - Currency types
- And more...

---

## README Maintenance

### Rule: README.md must be updated synchronously with code changes

#### Update Policy

1. **Version History**: Add new entry for each release with:
   - Version number (SemVer format: MAJOR.MINOR.PATCH)
   - Release date (YYYY-MM-DD format)
   - Concise description of changes

2. **Bilingual Consistency**: Both English and Chinese sections must be updated simultaneously

3. **API Changes**: Any change to public APIs must be reflected in:
   - Quick Start section
   - Usage scenarios
   - Code examples

4. **Structure Preservation**: Maintain standard Markdown formatting; do not restructure arbitrarily

#### Example Version Entry

```markdown
| 1.5.0 | 2024-06-01 | Added Schwab broker integration and enhanced error handling |
```

#### Last Updated
- Current version: 1.4.0
- Last updated: 2024-05-16

---

## Sensitive Data Protection

### Rule: Never commit sensitive data to GitHub

#### Protected Files (in `.gitignore`)
- `appsettings.json`
- `appsettings.*.json` (except `appsettings.example.json`)
- `*.secret`
- `*.env`
- `secrets.json`

#### Sensitive Data Types
- API keys and secrets
- Database connection strings
- Email credentials
- Broker authentication tokens
- Private keys and certificates

#### Best Practices

1. **Use Environment Variables**:
   ```csharp
   var apiKey = Environment.GetEnvironmentVariable("BINANCE_API_KEY");
   ```

2. **Use User Secrets (Development)**:
   ```bash
   dotnet user-secrets set "Exchange:ApiKey" "your-key-here"
   ```

3. **Use Example Files**:
   - Create `appsettings.example.json` with placeholder values
   - Document required configuration structure
   - Never include real credentials

4. **Review Before Commit**:
   - Check for accidental credential exposure
   - Use pre-commit hooks if available
   - Review git diff before pushing

---

## Code Review Checklist

Use this checklist when reviewing pull requests or your own code:

### Documentation
- [ ] All public members have bilingual XML documentation
- [ ] Documentation follows the Chinese + English format
- [ ] Parameters, return values, and exceptions are documented
- [ ] README is updated if public APIs changed

### Parameter Validation
- [ ] All public methods validate parameters at the beginning
- [ ] Null checks use `ArgumentNullException`
- [ ] String checks use `ArgumentException` with `nameof()`
- [ ] Range checks use `ArgumentOutOfRangeException`
- [ ] Error messages are in English

### Coding Standards
- [ ] Console/Log/Exception messages are in English
- [ ] No Chinese characters in runtime output
- [ ] Code comments are bilingual where helpful
- [ ] SOLID principles are followed

### Time Handling
- [ ] All persistence operations use `DateTime.UtcNow`
- [ ] No usage of `DateTime.Now` for timestamps
- [ ] Unix timestamp calculations use UTC

### Enums
- [ ] All enums are in `Shared/Model/Enums.cs`
- [ ] Enums have bilingual XML documentation
- [ ] Enum values have explicit integer assignments

### Security
- [ ] No sensitive data in code
- [ ] Configuration uses secure patterns
- [ ] `.gitignore` excludes sensitive files

### Testing
- [ ] Unit tests pass: `dotnet test`
- [ ] No breaking changes to existing functionality
- [ ] New features have corresponding tests

---

## Enforcement

### Automated Checks
Consider implementing:
- Roslyn Analyzers for XML documentation enforcement
- StyleCop rules for code style
- Pre-commit hooks for sensitive data detection
- CI/CD pipeline checks for test coverage

### Manual Reviews
- Code reviews must verify compliance with these standards
- Reject PRs that violate critical standards
- Provide constructive feedback for improvements

---

## Version History

| Version | Date | Description |
|---------|------|-------------|
| 1.0.0 | 2024-05-16 | Initial code standards document created |

---

## Questions?

If you have questions about these standards or need clarification:
- Open an issue on GitHub
- Contact: rex.fan18@gmail.com
- Join Telegram group: https://t.me/+VPy-VLis8gVmYWM1
