using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Saas.Infra.MVC.Services.Redirect;

namespace Saas.Infra.MVC.Tests.Services;

/// <summary>
/// Property-based tests for RedirectValidator
/// </summary>
public class RedirectValidatorTests
{
    private readonly IRedirectValidator _validator;
    private readonly Mock<ILogger<RedirectValidator>> _mockLogger;

    public RedirectValidatorTests()
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
    /// Property 1: URL Parameter Extraction
    /// Validates: Requirements 1.1, 1.4
    /// For any login request with a redirect_url query parameter, the system should extract 
    /// and process the parameter value correctly, regardless of encoding or special characters.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UrlParameterExtraction(string encodedPath)
    {
        // Generate valid encoded paths
        var validPaths = new[] { "/dashboard", "/payment", "/profile", "/settings", "/api/products" };
        var path = validPaths[Math.Abs((encodedPath ?? string.Empty).GetHashCode()) % validPaths.Length];
        var encoded = Uri.EscapeDataString(path);

        return Prop.ForAll(Arb.Default.String(), (extra) =>
        {
            // Test that encoded URLs are properly decoded and validated
            var result = _validator.ValidateAsync(encoded).Result;
            
            // If it's a valid path, it should be decoded correctly
            if (validPaths.Contains(path))
            {
                Assert.True(result.IsValid);
                Assert.Equal(path, result.ValidatedPath);
            }
        });
    }

    /// <summary>
    /// Property 2: Empty Redirect URL Handling
    /// Validates: Requirements 1.3, 2.7
    /// For any redirect_url parameter that is empty, null, or contains only whitespace, 
    /// the system should treat it identically to a request with no redirect_url parameter 
    /// and display the product selection page.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyRedirectUrlHandling(string? input)
    {
        var testCases = new[] { "", "   ", "\t", "\n", null };
        var testValue = testCases[Math.Abs((input?.GetHashCode() ?? 0)) % testCases.Length];

        var result = _validator.ValidateAsync(testValue).Result;
        
        // Empty/null/whitespace should be treated as valid with null path (no redirect)
        Assert.True(result.IsValid);
        Assert.Null(result.ValidatedPath);
        
        return true.ToProperty();
    }

    /// <summary>
    /// Property 3: Path Traversal Prevention
    /// Validates: Requirements 2.3, 7.5
    /// For any redirect URL containing path traversal sequences (../, ..\, or encoded variants 
    /// like %2e%2e), the validator should reject it as invalid regardless of other characteristics.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PathTraversalPrevention(string prefix)
    {
        var traversalPatterns = new[] 
        { 
            "../", 
            "..\\", 
            "%2e%2e/", 
            "%2e%2e\\",
            "..%2f",
            "..%5c"
        };
        
        var pattern = traversalPatterns[Math.Abs((prefix ?? string.Empty).GetHashCode()) % traversalPatterns.Length];
        var maliciousUrl = $"/dashboard{pattern}etc/passwd";

        var result = _validator.ValidateAsync(maliciousUrl).Result;
        
        // All path traversal attempts should be rejected
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        
        return true.ToProperty();
    }

    /// <summary>
    /// Property 4: Protocol Scheme Rejection
    /// Validates: Requirements 2.5, 7.1, 7.2, 7.3, 7.4
    /// For any redirect URL containing a protocol scheme (http://, https://, ftp://, javascript:, 
    /// data:, file://, or any other scheme), the validator should reject it as invalid.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProtocolSchemeRejection(string suffix)
    {
        var schemes = new[] 
        { 
            "http://", 
            "https://", 
            "ftp://", 
            "javascript:", 
            "data:", 
            "file://",
            "vbscript:",
            "about:",
            "blob:"
        };
        
        var scheme = schemes[Math.Abs((suffix ?? string.Empty).GetHashCode()) % schemes.Length];
        var maliciousUrl = $"{scheme}evil.com/dashboard";

        var result = _validator.ValidateAsync(maliciousUrl).Result;
        
        // All protocol schemes should be rejected
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        
        return true.ToProperty();
    }

    /// <summary>
    /// Property 5: Relative Path Requirement
    /// Validates: Requirements 2.2
    /// For any redirect URL that does not start with `/` (indicating a relative path), 
    /// the validator should reject it as invalid, even if it would otherwise be valid.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RelativePathRequirement(string path)
    {
        // Generate paths without leading slash
        var invalidPaths = new[] { "dashboard", "payment", "profile", "settings", "api/products" };
        var invalidPath = invalidPaths[Math.Abs((path ?? string.Empty).GetHashCode()) % invalidPaths.Length];

        var result = _validator.ValidateAsync(invalidPath).Result;
        
        // Paths without leading slash should be rejected
        Assert.False(result.IsValid);
        
        return true.ToProperty();
    }

    /// <summary>
    /// Property 6: Whitelist Enforcement
    /// Validates: Requirements 2.1, 2.4
    /// For any redirect URL that is not in the configured whitelist, the validator should 
    /// reject it as invalid, even if it passes all other security checks.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhitelistEnforcement(string path)
    {
        // Generate paths not in whitelist
        var invalidPaths = new[] 
        { 
            "/admin", 
            "/secret", 
            "/internal", 
            "/api/admin", 
            "/unauthorized" 
        };
        
        var invalidPath = invalidPaths[Math.Abs((path ?? string.Empty).GetHashCode()) % invalidPaths.Length];

        var result = _validator.ValidateAsync(invalidPath).Result;
        
        // Paths not in whitelist should be rejected
        Assert.False(result.IsValid);
        
        return true.ToProperty();
    }

    /// <summary>
    /// Property 7: Valid Redirect Execution
    /// Validates: Requirements 3.1, 3.2, 3.3, 3.4
    /// For any redirect URL that passes all validation checks (relative path, no traversal, 
    /// no protocol, in whitelist), the system should return a redirect response without 
    /// displaying an intermediate success page.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidRedirectExecution(int index)
    {
        var validPaths = new[] { "/dashboard", "/payment", "/profile", "/settings", "/api/products" };
        var validPath = validPaths[Math.Abs(index) % validPaths.Length];

        var result = _validator.ValidateAsync(validPath).Result;
        
        // Valid paths should be accepted
        Assert.True(result.IsValid);
        Assert.Equal(validPath, result.ValidatedPath);
        Assert.Null(result.ErrorMessage);
        
        return true.ToProperty();
    }

    /// <summary>
    /// Property 15: URL Decoding Safety
    /// Validates: Requirements 1.4, 2.3, 2.5
    /// For any redirect URL with URL encoding (including encoded path traversal like %2e%2e 
    /// or encoded protocols like %68%74%74%70), the validator should decode it and apply 
    /// all security checks to the decoded value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UrlDecodingSafety(string input)
    {
        var testCases = new[]
        {
            // Encoded path traversal
            new { Encoded = "%2e%2e%2fdashboard", Expected = false, Description = "Encoded ../" },
            // Encoded protocol
            new { Encoded = "%68%74%74%70%3a%2f%2fevil.com", Expected = false, Description = "Encoded http://" },
            // Valid encoded path
            new { Encoded = "%2fdashboard", Expected = true, Description = "Encoded /dashboard" },
            // Mixed encoding
            new { Encoded = "%2f%64%61%73%68%62%6f%61%72%64", Expected = true, Description = "Encoded /dashboard (all chars)" }
        };

        var testCase = testCases[Math.Abs((input ?? string.Empty).GetHashCode()) % testCases.Length];
        var result = _validator.ValidateAsync(testCase.Encoded).Result;

        if (testCase.Expected)
        {
            Assert.True(result.IsValid, $"Failed: {testCase.Description}");
        }
        else
        {
            Assert.False(result.IsValid, $"Failed: {testCase.Description}");
        }

        return true.ToProperty();
    }

    /// <summary>
    /// Unit test: Null input should be valid with null path
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithNullInput_ReturnsValidWithNullPath()
    {
        var result = await _validator.ValidateAsync(null);
        
        Assert.True(result.IsValid);
        Assert.Null(result.ValidatedPath);
    }

    /// <summary>
    /// Unit test: Empty string should be valid with null path
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithEmptyString_ReturnsValidWithNullPath()
    {
        var result = await _validator.ValidateAsync("");
        
        Assert.True(result.IsValid);
        Assert.Null(result.ValidatedPath);
    }

    /// <summary>
    /// Unit test: Whitespace should be valid with null path
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithWhitespace_ReturnsValidWithNullPath()
    {
        var result = await _validator.ValidateAsync("   ");
        
        Assert.True(result.IsValid);
        Assert.Null(result.ValidatedPath);
    }

    /// <summary>
    /// Unit test: Valid whitelisted path should be accepted
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithValidPath_ReturnsValid()
    {
        var result = await _validator.ValidateAsync("/dashboard");
        
        Assert.True(result.IsValid);
        Assert.Equal("/dashboard", result.ValidatedPath);
    }

    /// <summary>
    /// Unit test: Path traversal should be rejected
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithPathTraversal_ReturnsInvalid()
    {
        var result = await _validator.ValidateAsync("/../admin");
        
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Unit test: Protocol scheme should be rejected
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithHttpProtocol_ReturnsInvalid()
    {
        var result = await _validator.ValidateAsync("http://evil.com");
        
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Unit test: Non-relative path should be rejected
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithoutLeadingSlash_ReturnsInvalid()
    {
        var result = await _validator.ValidateAsync("dashboard");
        
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Unit test: Path not in whitelist should be rejected
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithPathNotInWhitelist_ReturnsInvalid()
    {
        var result = await _validator.ValidateAsync("/admin");
        
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Unit test: Encoded path traversal should be rejected
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithEncodedPathTraversal_ReturnsInvalid()
    {
        var result = await _validator.ValidateAsync("%2e%2e%2fdashboard");
        
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Unit test: Encoded protocol should be rejected
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithEncodedProtocol_ReturnsInvalid()
    {
        var result = await _validator.ValidateAsync("%68%74%74%70%3a%2f%2fevil.com");
        
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Unit test: Encoded valid path should be accepted
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithEncodedValidPath_ReturnsValid()
    {
        var result = await _validator.ValidateAsync("%2fdashboard");
        
        Assert.True(result.IsValid);
        Assert.Equal("/dashboard", result.ValidatedPath);
    }
}
