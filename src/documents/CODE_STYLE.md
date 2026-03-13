CODE STYLE / 代码规范

目的 / Purpose
- 为本仓库建立统一的代码规范，保证可维护性、可读性，防止误操作（例如误调真实交易）。
- Provide unified coding guidelines to improve maintainability and prevent accidental operations (e.g. real trading).

总体要求 / High-level rules
- 遵循 SOLID 原则。
- Keep It Simple, Stupid (KISS)。
- 所有 public 方法在方法头部必须进行入参有效性校验，不合格参数需抛出适当异常（例如 ArgumentNullException / ArgumentException）。
- 所有方法和类必须包含中、英文 XML 注释（Summary 至少包含中文与英文简短说明；对重要入参与返回值写明含义）。
- 所有 `Console.WriteLine` 级别输出必须使用英文字符串。
- 修改代码时不得改变原有业务逻辑；只做风格/注释/轻微防御性校验性改动。

示例 / Examples
- 参数校验（C#）:

```csharp
public void DoWork(string name, object options)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("name must not be null or whitespace", nameof(name));
    if (options is null)
        throw new ArgumentNullException(nameof(options));

    // 业务逻辑（不可改变）
}
```

- 中英文 XML 注释示例:

```csharp
/// <summary>
/// 计算价差
/// Calculate spread between two series.
/// </summary>
/// <param name="seriesA">第一个数据序列 / First series</param>
/// <param name="seriesB">第二个数据序列 / Second series</param>
/// <returns>价差序列 / Spread series</returns>
public IEnumerable<double> CalculateSpread(IEnumerable<double> seriesA, IEnumerable<double> seriesB)
{
    // ...
}
```

检查与工具 / Checks and Tools
- 推荐在本地/CI 中开启静态分析（Roslyn analyzers / StyleCop / Microsoft.CodeAnalysis.NetAnalyzers），并把缺失 XML 文档与入参校验规则设为警告或错误。
- 可使用 `.editorconfig` 来设定分析规则优先级。

注意事项 / Notes
- 所有会直接调用真实交易的代码应被抽象（例如通过 `ITradingService`）并在生产配置里明确打开；默认情况下应使用沙盒或 mock。
- 在进行大范围自动替换前，请先对关键功能运行单元测试。

如果你想让我把这些规则自动化（添加 `.editorconfig`、在 csproj 中引入 analyzers 或批量修改文件），我可以继续执行。