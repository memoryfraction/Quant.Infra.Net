using Microsoft.Extensions.Configuration;
using Saas.Infra.Core;
using Saas.Infra.Services.Payment;
using System.Collections.Generic;

namespace Saas.Infra.Net.Tests.Payment
{
    /// <summary>
    /// PaymentUrlResolver 单元测试。
    /// Unit tests for PaymentUrlResolver.
    /// </summary>
    [TestClass]
    [DoNotParallelize] // Environment variables are process-level shared state
    public class PaymentUrlResolverTests
    {
        private string? _originalContainerAppName;
        private string? _originalContainerAppHostname;

        [TestInitialize]
        public void Setup()
        {
            _originalContainerAppName = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME");
            _originalContainerAppHostname = Environment.GetEnvironmentVariable("CONTAINER_APP_HOSTNAME");

            Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", null);
            Environment.SetEnvironmentVariable("CONTAINER_APP_HOSTNAME", null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", _originalContainerAppName);
            Environment.SetEnvironmentVariable("CONTAINER_APP_HOSTNAME", _originalContainerAppHostname);
        }

        /// <summary>
        /// 测试在 Azure Container Apps 环境下，优先从 CONTAINER_APP_HOSTNAME 环境变量解析 URL。
        /// Tests that in an Azure Container Apps environment, the URL is primarily resolved from the CONTAINER_APP_HOSTNAME environment variable.
        /// </summary>
        [TestMethod]
        public void ResolveBaseUrl_AcaEnvironment_ShouldUseContainerAppHostname()
        {
            // Arrange
            Environment.SetEnvironmentVariable("CONTAINER_APP_NAME", "saas-app");
            Environment.SetEnvironmentVariable("CONTAINER_APP_HOSTNAME", "saas-app.eastasia.azurecontainerapps.io");
            
            var config = new ConfigurationBuilder().Build();
            var resolver = new PaymentUrlResolver(config);

            // Act
            var result = resolver.ResolveBaseUrl("http", "localhost");

            // Assert
            Assert.AreEqual("https://saas-app.eastasia.azurecontainerapps.io", result);
        }

        /// <summary>
        /// 测试当配置了 Payment:PublicBaseUrl 时，应该从配置解析 URL。
        /// Tests that when Payment:PublicBaseUrl is configured, the URL should be resolved from the configuration.
        /// </summary>
        [TestMethod]
        public void ResolveBaseUrl_Configured_ShouldUseConfiguredUrl()
        {
            // Arrange
            var settings = new Dictionary<string, string?>
            {
                {"Payment:PublicBaseUrl", "https://manual.com"}
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            var resolver = new PaymentUrlResolver(config);

            // Act
            var result = resolver.ResolveBaseUrl("http", "localhost");

            // Assert
            Assert.AreEqual("https://manual.com", result);
        }

        /// <summary>
        /// 测试最后回退到当前请求的 Scheme 和 Host 进行解析。
        /// Tests that last fallback resolves to the current request's Scheme and Host.
        /// </summary>
        [TestMethod]
        public void ResolveBaseUrl_NoAcaNoConfig_ShouldUseRequestContext()
        {
            // Arrange
            var config = new ConfigurationBuilder().Build();
            var resolver = new PaymentUrlResolver(config);

            // Act
            var result = resolver.ResolveBaseUrl("https", "myhost.com:5001");

            // Assert
            Assert.AreEqual("https://myhost.com:5001", result);
        }
    }
}
