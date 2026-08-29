using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Quant.Infra.Net.Shared.Model;

namespace Quant.Infra.Net.Runtime.Internal;

/// <summary>
/// 仅承载 broker 凭据三键（Binance:ApiKey / Binance:ApiSecret / Binance:Environment）的极简
/// <see cref="IConfigurationSection"/> 实现，用于适配核心库 <c>BinanceUsdFutureService(IConfiguration)</c>
/// 构造函数（调用方侧适配，不新增包依赖；不实现 Reload/Children 等重语义）。
/// A minimal IConfigurationSection implementation carrying only the three broker credential keys, used to
/// adapt the core BinanceUsdFutureService(IConfiguration) constructor (call-site adaptation; no new package
/// dependency; heavy semantics like Reload/Children are unimplemented by design).
/// </summary>
internal sealed class BrokerConfiguration : IConfigurationSection
{
    private readonly Dictionary<string, string> _values;

    private BrokerConfiguration(Dictionary<string, string> values, string? key = null, string? value = null)
    {
        _values = values;
        Key = key!;
        Value = value;
    }

    /// <summary>
    /// 由 broker 凭据与运行环境构造 / Constructs the configuration from broker credentials and environment.
    /// </summary>
    public static BrokerConfiguration ForBroker(string apiKey, string apiSecret, ExchangeEnvironment environment)
        => new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Binance:ApiKey"] = apiKey,
            ["Binance:ApiSecret"] = apiSecret,
            ["Binance:Environment"] = environment.ToString()
        });

    /// <summary>本节键（根节为 null）/ Section key (null at the root).</summary>
    public string Key { get; }

    /// <summary>本节完整路径（根节为空）/ Full section path (empty at the root).</summary>
    public string Path => Key ?? string.Empty;

    /// <summary>本节值（根节为 null）/ Section value (null at the root).</summary>
    public string? Value { get; set; }

    /// <summary>根节恒存在 / The root section always exists.</summary>
    public bool Exists() => Key is null || _values.ContainsKey(Key);

    /// <summary>按键取值（不存在为 null）/ Value lookup by key (null when absent).</summary>
    public string? this[string key]
    {
        get => _values.TryGetValue(key, out var value) ? value : null;
        set => _values[key] = value!;
    }

    /// <summary>子节只返回键值（不产生子树）/ Child sections return the plain value (no subtree).</summary>
    public IConfigurationSection GetSection(string key)
        => new BrokerConfiguration(_values, key, _values.TryGetValue(key, out var v) ? v : null);

    /// <summary>无子节 / No children.</summary>
    public IEnumerable<IConfigurationSection> GetChildren()
        => Enumerable.Empty<IConfigurationSection>();

    /// <summary>不可重载 / Not reloadable.</summary>
    public IChangeToken GetReloadToken() => NoChangeToken.Instance;

    /// <summary>枚举全部键值 / Enumerates all key-value pairs.</summary>
    public IEnumerable<KeyValuePair<string, string?>> AsEnumerable()
        => _values.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value));

    /// <summary>不可重载 / Not reloadable.</summary>
    public void Reload()
    {
    }

    /// <summary>
    /// 永不变化的 <see cref="IChangeToken"/>（9.0 中 StaticChangeToken 已移除，故内置一个最小实现）。
    /// A never-changing IChangeToken (StaticChangeToken was removed in 9.0; minimal built-in implementation).
    /// </summary>
    private sealed class NoChangeToken : IChangeToken
    {
        /// <summary>单例 / The singleton instance.</summary>
        public static readonly NoChangeToken Instance = new();

        /// <summary>永不变化 / Never changes.</summary>
        public bool HasChanged => false;

        /// <summary>无活动回调 / No active callbacks.</summary>
        public bool ActiveChangeCallbacks => false;

        /// <summary>无回调 / No callback.</summary>
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
            => throw new NotSupportedException("This change token never fires.");
    }
}
