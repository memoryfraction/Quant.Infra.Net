namespace Quant.Infra.Net.Shared;

/// <summary>
/// 中文：License 验证全局常量配置，所有 License 相关的配置键名、默认值、约束范围统一存放于此。
/// 修改一处即全局生效，降低维护成本。
/// English: Global constants for license validation. All license-related config keys, default values,
/// and constraint ranges are centralized here. Change one place, apply everywhere.
/// </summary>
public static class LicenseConstants
{
    // ============================================================
    // 配置段名称（Configuration Section Names）
    // ============================================================

    /// <summary>
    /// 中文：License 配置段，对应 appsettings.json 中的 "License" 节点。
    /// English: License config section, corresponding to the "License" node in appsettings.json.
    /// </summary>
    public const string SectionName = "License";

    /// <summary>
    /// 中文：LicenseForge 配置段，对应 appsettings.json 中的 "LicenseForge" 节点。
    /// English: LicenseForge config section, corresponding to the "LicenseForge" node in appsettings.json.
    /// </summary>
    public const string LicenseForgeSectionName = "LicenseForge";

    // ============================================================
    // 配置键名称（Configuration Key Paths）
    // 格式为 "Section:Key"，可直接用于 IConfiguration 索引器
    // ============================================================

    /// <summary>
    /// 中文：License 授权码配置键。
    /// English: License key config key.
    /// </summary>
    public const string KeyLicenseKey = "License:Key";

    /// <summary>
    /// 中文：产品代码配置键。
    /// English: Product code config key.
    /// </summary>
    public const string KeyProductCode = "License:ProductCode";

    /// <summary>
    /// 中文：心跳间隔（小时）配置键。
    /// English: Heartbeat interval (hours) config key.
    /// </summary>
    public const string KeyHeartbeatIntervalHours = "License:HeartbeatIntervalHours";

    /// <summary>
    /// 中文：LicenseForge 基础 URL 配置键。
    /// English: LicenseForge base URL config key.
    /// </summary>
    public const string KeyLicenseForgeBaseUrl = "LicenseForge:BaseUrl";

    // ============================================================
    // 默认值（Default Values）
    // ============================================================

    /// <summary>
    /// 中文：LicenseForge API 默认地址（Azure Container Apps）。
    /// English: Default LicenseForge API base URL (Azure Container Apps).
    /// </summary>
    public const string DefaultLicenseForgeBaseUrl =
        "https://license-forge-app.greengrass-8e23c1df.westus.azurecontainerapps.io";

    /// <summary>
    /// 中文：默认产品代码。
    /// English: Default product code.
    /// </summary>
    public const string DefaultProductCode = "quantinfra-pro";

    /// <summary>
    /// 中文：默认心跳间隔（小时）。
    /// English: Default heartbeat interval (hours).
    /// </summary>
    public const double DefaultHeartbeatIntervalHours = 3.0;

    // ============================================================
    // 约束范围（Constraint Ranges）
    // ============================================================

    /// <summary>
    /// 中文：心跳间隔最小值（小时），防止过于频繁增加 ACA 成本。
    /// English: Minimum heartbeat interval (hours), to prevent excessive ACA cost.
    /// </summary>
    public const double MinHeartbeatIntervalHours = 1.0;

    /// <summary>
    /// 中文：心跳间隔最大值（小时），防止授权失效后检测延迟过长。
    /// English: Maximum heartbeat interval (hours), to prevent delayed detection of expired license.
    /// </summary>
    public const double MaxHeartbeatIntervalHours = 6.0;

    /// <summary>
    /// 中文：Polly 重试延迟序列（秒），应对 ACA 冷启动 30-90s。总等待 100s 覆盖最慢冷启动。
    /// English: Polly retry delay sequence (seconds), to handle ACA cold start 30-90s. Total wait 100s covers the slowest cold start.
    /// </summary>
    public static readonly TimeSpan[] RetryDelays = new[]
    {
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(40)
    };

    // ============================================================
    // 辅助方法（Helper Methods）
    // ============================================================

    /// <summary>
    /// 中文：将配置值限制在有效范围内并返回。如果未配置则返回默认值。
    /// English: Clamp the configured value within valid range, or return default if not set.
    /// </summary>
    public static double GetHeartbeatIntervalHours(double? configuredValue)
    {
        return configuredValue.HasValue
            ? Math.Clamp(configuredValue.Value, MinHeartbeatIntervalHours, MaxHeartbeatIntervalHours)
            : DefaultHeartbeatIntervalHours;
    }

    /// <summary>
    /// 中文：获取 ICS (httpClientFactory) 中用于 License 心跳的命名 HttpClient 名称。
    /// English: Get the named HttpClient name used for license heartbeat in DI.
    /// </summary>
    public const string HttpClientName = "LicenseHeartbeat";

    // ============================================================
    // 联系信息（Contact Information）
    // ============================================================

    /// <summary>
    /// 中文：客服邮箱，供用户在授权问题、技术支持时联系。
    /// English: Customer support email for license issues and technical support.
    /// </summary>
    public const string SupportEmail = "alphawealthlab@outlook.com";

    /// <summary>
    /// 中文：客服名称，用于 Swagger 联系信息等场景。
    /// English: Support contact name, used in Swagger contact info etc.
    /// </summary>
    public const string SupportName = "Alpha Wealth Lab Support";
}
