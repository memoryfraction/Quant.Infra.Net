using Saas.Infra.Core;

namespace Saas.Infra.Net.Tests;

/// <summary>
/// UtilityService.GetCurrentEnvironment 单元测试。
/// Unit tests for UtilityService.GetCurrentEnvironment.
/// </summary>
[TestClass]
[DoNotParallelize] // 环境变量是进程级共享状态，测试必须串行执行
public class GetCurrentEnvironmentTests
{
    /// <summary>
    /// 保存测试前的环境变量值，测试后还原。
    /// Saves original environment variable values before each test and restores them after.
    /// </summary>
    private string? _originalContainerAppName;
    private string? _originalDotnetRunningInContainer;

    [TestInitialize]
    public void Setup()
    {
        _originalContainerAppName = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME");
        _originalDotnetRunningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", _originalContainerAppName);
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", _originalDotnetRunningInContainer);
    }

    /// <summary>
    /// 当 CONTAINER_APP_NAME 已设置时，应返回 AzureContainerApps。
    /// Should return AzureContainerApps when CONTAINER_APP_NAME is set.
    /// </summary>
    [TestMethod]
    public void GetCurrentEnvironment_ShouldReturnAzureContainerApps_WhenContainerAppNameIsSet()
    {
        Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", "my-aca-app");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", null);

        var result = UtilityService.GetCurrentEnvironment();

        Assert.AreEqual(RuntimeEnvironment.AzureContainerApps, result);
    }

    /// <summary>
    /// ACA 优先级高于容器检测：同时设置 CONTAINER_APP_NAME 和 DOTNET_RUNNING_IN_CONTAINER 时应返回 AzureContainerApps。
    /// ACA takes priority: should return AzureContainerApps when both env vars are set.
    /// </summary>
    [TestMethod]
    public void GetCurrentEnvironment_ShouldReturnAzureContainerApps_WhenBothAcaAndContainerVarsAreSet()
    {
        Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", "my-aca-app");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

        var result = UtilityService.GetCurrentEnvironment();

        Assert.AreEqual(RuntimeEnvironment.AzureContainerApps, result);
    }

    /// <summary>
    /// 当 DOTNET_RUNNING_IN_CONTAINER=true 但无 ACA 标识时，应返回 LocalContainer。
    /// Should return LocalContainer when DOTNET_RUNNING_IN_CONTAINER is true but no ACA marker.
    /// </summary>
    [TestMethod]
    public void GetCurrentEnvironment_ShouldReturnLocalContainer_WhenDotnetRunningInContainerIsTrue()
    {
        Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", null);
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

        var result = UtilityService.GetCurrentEnvironment();

        Assert.AreEqual(RuntimeEnvironment.LocalContainer, result);
    }

    /// <summary>
    /// 在 Windows 本地（无容器环境变量）时应返回 LocalWindows。
    /// Should return LocalWindows on a Windows machine with no container env vars.
    /// </summary>
    [TestMethod]
    public void GetCurrentEnvironment_ShouldReturnLocalWindows_WhenOnWindowsWithoutContainer()
    {
        Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", null);
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", null);

        var result = UtilityService.GetCurrentEnvironment();

        // 在 Windows CI/本地运行时应为 LocalWindows；在 Linux CI 上为 OtherLinux
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            Assert.AreEqual(RuntimeEnvironment.LocalWindows, result);
        }
        else
        {
            Assert.AreEqual(RuntimeEnvironment.OtherLinux, result);
        }
    }

    /// <summary>
    /// 返回值必须是 RuntimeEnvironment 枚举的有效成员。
    /// The return value must be a valid member of RuntimeEnvironment enum.
    /// </summary>
    [TestMethod]
    public void GetCurrentEnvironment_ShouldReturnValidEnumValue()
    {
        var result = UtilityService.GetCurrentEnvironment();

        Assert.IsTrue(Enum.IsDefined(typeof(RuntimeEnvironment), result),
            $"Returned value {result} is not a defined RuntimeEnvironment member.");
    }

    /// <summary>
    /// CONTAINER_APP_NAME 为空字符串时不应视为 ACA 环境。
    /// Empty CONTAINER_APP_NAME should not be treated as ACA environment.
    /// </summary>
    [TestMethod]
    public void GetCurrentEnvironment_ShouldNotReturnAca_WhenContainerAppNameIsEmpty()
    {
        Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", "");
        Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", null);

        var result = UtilityService.GetCurrentEnvironment();

        Assert.AreNotEqual(RuntimeEnvironment.AzureContainerApps, result);
    }
}
