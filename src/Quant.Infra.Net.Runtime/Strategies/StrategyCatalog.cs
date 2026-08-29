using System.Reflection;

namespace Quant.Infra.Net.Runtime.Strategies;

/// <summary>
/// 策略目录：反射扫描指定程序集里的全部 <see cref="IStrategyDescriptor"/> 实现，按 Name 建索引（设计 §7.6）。
/// 本类所在程序集（内置 3 个策略描述符）自动加入扫描集合，无需显式传入。
/// Strategy catalog: reflection-scans the given assemblies for all IStrategyDescriptor implementations, indexed by Name
/// (design section 7.6). The declaring assembly (holding the 3 built-in descriptors) is scanned automatically.
/// </summary>
/// <remarks>
/// 重名策略（大小写不敏感）在构造时即抛出 <see cref="InvalidOperationException"/>（fail-fast）。
/// Duplicate strategy names (case-insensitive) throw InvalidOperationException at construction (fail-fast).
/// </remarks>
public sealed class StrategyCatalog
{
    private readonly Dictionary<string, IStrategyDescriptor> _descriptors;

    /// <summary>
    /// 反射扫描各程序集并建立按名字索引的目录。
    /// Scans the assemblies and builds the name-indexed catalog.
    /// </summary>
    /// <param name="assemblies">要扫描的程序集（不得为 null；空集合表示只扫描内置描述符所在程序集）/ Assemblies to scan (must not be null; empty = built-in assembly only).</param>
    /// <exception cref="ArgumentNullException">assemblies 为 null 或其元素为 null 时抛出 / Thrown when assemblies or an element is null.</exception>
    /// <exception cref="InvalidOperationException">发现重名策略时抛出（fail-fast）/ Thrown when duplicate strategy names are found (fail-fast).</exception>
    public StrategyCatalog(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var targets = new List<Assembly> { typeof(StrategyCatalog).Assembly };
        foreach (var assembly in assemblies)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assemblies), "an assembly element is null");
            }

            if (!targets.Contains(assembly))
            {
                targets.Add(assembly);
            }
        }

        var found = new List<IStrategyDescriptor>();
        var map = new Dictionary<string, IStrategyDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in targets)
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (!type.IsClass || type.IsAbstract || type.IsInterface || !typeof(IStrategyDescriptor).IsAssignableFrom(type))
                {
                    continue;
                }

                object? instance;
                try
                {
                    instance = Activator.CreateInstance(type);
                }
                catch (MissingMethodException ex)
                {
                    throw new InvalidOperationException($"Strategy descriptor '{type.FullName}' must have a public parameterless constructor.", ex);
                }

                var descriptor = (IStrategyDescriptor?)instance
                    ?? throw new InvalidOperationException($"Strategy descriptor '{type.FullName}' could not be instantiated.");

                if (!map.TryAdd(descriptor.Name, descriptor))
                {
                    throw new InvalidOperationException(
                        $"Duplicate strategy name '{descriptor.Name}': '{map[descriptor.Name].GetType().FullName}' and '{descriptor.GetType().FullName}'.");
                }

                found.Add(descriptor);
            }
        }

        _descriptors = map;
        Names = found
            .Select(d => d.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 全部已发现的策略名（小写不敏感排序）。
    /// All discovered strategy names (case-insensitive order).
    /// </summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>
    /// 按名字解析描述符；未找到抛 <see cref="ArgumentException"/>（信息中列出全部可用策略名，fail-fast）。
    /// Resolves a descriptor by name (case-insensitive); throws ArgumentException listing all available names when not found (fail-fast).
    /// </summary>
    /// <param name="name">策略名（不得为 null 或空白）/ Strategy name (must not be null or blank).</param>
    /// <returns>对应的策略描述符 / The matching descriptor.</returns>
    /// <exception cref="ArgumentNullException">name 为 null 时抛出 / Thrown when name is null.</exception>
    /// <exception cref="ArgumentException">name 为空白或未知时抛出 / Thrown when name is blank or unknown.</exception>
    public IStrategyDescriptor Resolve(string? name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Strategy name must not be blank.", nameof(name));
        }

        if (_descriptors.TryGetValue(name, out var descriptor))
        {
            return descriptor;
        }

        throw new ArgumentException(
            $"Unknown strategy '{name}'. Available strategies: {string.Join(" | ", Names)}.",
            nameof(name));
    }
}
