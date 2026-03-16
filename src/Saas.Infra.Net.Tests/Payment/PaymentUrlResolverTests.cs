using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Saas.Infra.Services.Payment;

namespace Saas.Infra.Net.Tests.Payment;

/// <summary>
/// PaymentUrlResolver 单元测试。
/// Unit tests for PaymentUrlResolver.
/// </summary>
[TestClass]
[DoNotParallelize]
public class PaymentUrlResolverTests
{
    private string? _originalContainerAppName;
    private string? _originalContainerAppHostname;

    /// <summary>
    /// 保存原始环境变量并清空测试上下文。
    /// Saves original environment variables and clears the test context.
    /// </summary>
    [TestInitialize]
    public void Setup()
    {
        _originalContainerAppName = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME");
        _originalContainerAppHostname = Environment.GetEnvironmentVariable("CONTAINER_APP_HOSTNAME");

        Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", null);
        Environment.SetEnvironmentVariable("CONTAINER_APP_HOSTNAME", null);
    }

    /// <summary>
    /// 恢复原始环境变量。
    /// Restores original environment variables.
    /// </summary>
    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", _originalContainerAppName);
        Environment.SetEnvironmentVariable("CONTAINER_APP_HOSTNAME", _originalContainerAppHostname);
    }

    /// <summary>
    /// 测试 ACA 环境下非法主机名会降级到请求上下文。
    /// Tests that an invalid ACA hostname falls back to the request context.
    /// </summary>
    [TestMethod]
    public void ResolveBaseUrl_AcaEnvironment_InvalidHostname_ShouldFallbackToRequest()
    {
        Environment.SetEnvironmentVariable("CONTAINER_APP_HOSTNAME", "invalid-hostname-without-domain");
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);
        var testScheme = "https";
        var testHost = "myhost.com:8080";

        var result = resolver.ResolveBaseUrl(testScheme, testHost);

        Assert.AreEqual($"{testScheme}://{testHost}", result);
    }

    /// <summary>
    /// 测试 ACA 环境下空主机名会降级到请求上下文。
    /// Tests that an empty ACA hostname falls back to the request context.
    /// </summary>
    [TestMethod]
    public void ResolveBaseUrl_AcaEnvironment_EmptyHostname_ShouldFallbackToRequest()
    {
        Environment.SetEnvironmentVariable("CONTAINER_APP_HOSTNAME", string.Empty);
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);
        var testScheme = "http";
        var testHost = "localhost:5001";

        var result = resolver.ResolveBaseUrl(testScheme, testHost);

        Assert.AreEqual($"{testScheme}://{testHost}", result);
    }

    /// <summary>
    /// 测试标准请求上下文会返回正确 URL。
    /// Tests that a standard request context returns the correct URL.
    /// </summary>
    [TestMethod]
    public void ResolveBaseUrl_NoAca_StandardRequestContext_ShouldReturnCorrectUrl()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);
        var testScheme = "https";
        var testHost = "app.example.com";

        var result = resolver.ResolveBaseUrl(testScheme, testHost);

        Assert.AreEqual($"{testScheme}://{testHost}", result);
    }

    /// <summary>
    /// 测试带端口的请求上下文会保留端口。
    /// Tests that a request context with a port preserves the port.
    /// </summary>
    [TestMethod]
    public void ResolveBaseUrl_NoAca_RequestContextWithPort_ShouldPreservePort()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);
        var testScheme = "http";
        var testHost = "localhost:5000";

        var result = resolver.ResolveBaseUrl(testScheme, testHost);

        Assert.AreEqual($"{testScheme}://{testHost}", result);
    }

    /// <summary>
    /// 测试混合大小写协议会被标准化为小写。
    /// Tests that a mixed-case scheme is normalized to lowercase.
    /// </summary>
    [TestMethod]
    public void ResolveBaseUrl_NoAca_MixedCaseScheme_ShouldNormalizeToLower()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);
        var testScheme = "HtTpS";
        var testHost = "example.com";

        var result = resolver.ResolveBaseUrl(testScheme, testHost);

        Assert.AreEqual("https://example.com", result);
    }

    /// <summary>
    /// 测试主机名前后空白会被裁剪。
    /// Tests that surrounding whitespace in the host is trimmed.
    /// </summary>
    [TestMethod]
    public void ResolveBaseUrl_NoAca_HostWithWhitespace_ShouldTrimWhitespace()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);
        var testScheme = "https";
        var testHost = "  example.com:8080  ";

        var result = resolver.ResolveBaseUrl(testScheme, testHost);

        Assert.AreEqual("https://example.com:8080", result);
    }

    /// <summary>
    /// 测试空协议会抛出参数异常。
    /// Tests that an empty scheme throws an argument exception.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ResolveBaseUrl_EmptyScheme_ShouldThrowArgumentException()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);

        resolver.ResolveBaseUrl(string.Empty, "localhost");
    }

    /// <summary>
    /// 测试空白协议会抛出参数异常。
    /// Tests that a whitespace scheme throws an argument exception.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ResolveBaseUrl_WhitespaceScheme_ShouldThrowArgumentException()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);

        resolver.ResolveBaseUrl("   ", "localhost");
    }

    /// <summary>
    /// 测试空主机会抛出参数异常。
    /// Tests that an empty host throws an argument exception.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ResolveBaseUrl_EmptyHost_ShouldThrowArgumentException()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);

        resolver.ResolveBaseUrl("https", string.Empty);
    }

    /// <summary>
    /// 测试空白主机会抛出参数异常。
    /// Tests that a whitespace host throws an argument exception.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ResolveBaseUrl_WhitespaceHost_ShouldThrowArgumentException()
    {
        var config = new ConfigurationBuilder().Build();
        var resolver = new PaymentUrlResolver(config);

        resolver.ResolveBaseUrl("https", "   ");
    }
}
