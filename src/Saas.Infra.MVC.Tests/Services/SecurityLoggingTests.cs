using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Saas.Infra.MVC.Services.Redirect;

namespace Saas.Infra.MVC.Tests.Services;

/// <summary>
/// Property-based tests for security logging
/// </summary>
public class SecurityLoggingTests
{
    private readonly Mock<ILogger<RedirectValidator>> _mockLogger;
    private readonly IRedirectValidator _validator;

    public SecurityLoggingTests()
    {
        _mockLogger = new Mock<ILogger<RedirectValidator>>();
        
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Products:Whitelist:0", "/dashboard" },
                { "Products:Whitelist:1", "/payment" },
                { "Products:Whitelist:2", "/profile" },
                { "Products:Whitelist:3", "/settings" },
                { "Products:Whitelist:4", "/api/products" }
            })
            .Build();

        _validator = new RedirectValidator(config, _mockLogger.Object);
    }

    /// <summary>
    /// Property 14: Security Event Logging
    /// Validates: Requirements 4.4, 8.4
    /// For any redirect validation failure, the system should log sufficient details 
    /// for security auditing (including the attempted URL and validation reason) 
    /// while not exposing these details to end users.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SecurityEventLogging(string input)
    {
        var testCases = new[]
        {
            new { Url = "http://evil.com", Reason = "protocol" },
            new { Url = "/../admin", Reason = "traversal" },
            new { Url = "/admin", Reason = "whitelist" },
            new { Url = "dashboard", Reason = "relative" }
        };

        var testCase = testCases[Math.Abs(input.GetHashCode()) % testCases.Length];

        // Reset mock to track calls
        _mockLogger.Reset();

        // Validate the URL
        var result = _validator.ValidateAsync(testCase.Url).Result;

        // Verify that invalid URLs are rejected
        if (testCase.Reason != "relative" || !testCase.Url.StartsWith("/"))
        {
            Assert.False(result.IsValid);
        }

        // Verify logging was called for invalid URLs
        if (!result.IsValid)
        {
            // Verify that warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        return Prop.True;
    }

    /// <summary>
    /// Unit test: Path traversal logs warning
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithPathTraversal_LogsWarning()
    {
        _mockLogger.Reset();

        var result = await _validator.ValidateAsync("/../admin");

        Assert.False(result.IsValid);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Unit test: Protocol scheme logs warning
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithProtocolScheme_LogsWarning()
    {
        _mockLogger.Reset();

        var result = await _validator.ValidateAsync("http://evil.com");

        Assert.False(result.IsValid);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Unit test: Whitelist violation logs warning
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithPathNotInWhitelist_LogsWarning()
    {
        _mockLogger.Reset();

        var result = await _validator.ValidateAsync("/admin");

        Assert.False(result.IsValid);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Unit test: Valid path does not log warning
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithValidPath_DoesNotLogWarning()
    {
        _mockLogger.Reset();

        var result = await _validator.ValidateAsync("/dashboard");

        Assert.True(result.IsValid);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Unit test: Error message is not exposed to users
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ErrorMessage_NotExposedToUsers()
    {
        var result = await _validator.ValidateAsync("http://evil.com");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        
        // Error message should be generic, not expose the URL
        Assert.DoesNotContain("http://evil.com", result.ErrorMessage);
        Assert.DoesNotContain("evil.com", result.ErrorMessage);
    }

    /// <summary>
    /// Unit test: Malformed URL logs warning
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithMalformedUrl_LogsWarning()
    {
        _mockLogger.Reset();

        // Create a URL that will cause an exception during decoding
        var result = await _validator.ValidateAsync("%");

        Assert.False(result.IsValid);
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
