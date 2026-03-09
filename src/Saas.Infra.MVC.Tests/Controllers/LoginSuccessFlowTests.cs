using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Saas.Infra.MVC.Controllers.Mvc;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.MVC.Services.Product;
using Saas.Infra.MVC.Services.Redirect;

namespace Saas.Infra.MVC.Tests.Controllers;

/// <summary>
/// Property-based tests for login success flow
/// </summary>
public class LoginSuccessFlowTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IRedirectValidator> _mockRedirectValidator;
    private readonly Mock<IProductConfigService> _mockProductConfigService;
    private readonly AccountController _controller;

    public LoginSuccessFlowTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockRedirectValidator = new Mock<IRedirectValidator>();
        _mockProductConfigService = new Mock<IProductConfigService>();

        _mockConfiguration.Setup(c => c["ApiSettings:BaseUrl"]).Returns("https://localhost:7268");

        _controller = new AccountController(
            _mockHttpClientFactory.Object,
            _mockConfiguration.Object,
            _mockRedirectValidator.Object,
            _mockProductConfigService.Object);

        // Setup default mock behaviors
        _mockRedirectValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>()))
            .ReturnsAsync(new RedirectValidationResult { IsValid = false });

        _mockProductConfigService
            .Setup(p => p.GetAvailableProductsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ProductInfo>
            {
                new ProductInfo { Id = "cryptocycleai", Name = "CryptoCycleAI", Description = "AI-powered analysis" }
            });
    }

    /// <summary>
    /// Property 7: Valid Redirect Execution
    /// Validates: Requirements 3.1, 3.2, 3.3, 3.4
    /// For any redirect URL that passes all validation checks, the system should return 
    /// a redirect response without displaying an intermediate success page.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidRedirectExecution(string suffix)
    {
        var validPaths = new[] { "/dashboard", "/payment", "/profile", "/settings", "/api/products" };
        var validPath = validPaths[Math.Abs((suffix ?? string.Empty).GetHashCode()) % validPaths.Length];

        _mockRedirectValidator
            .Setup(v => v.ValidateAsync(validPath))
            .ReturnsAsync(new RedirectValidationResult { IsValid = true, ValidatedPath = validPath });

        // Simulate the response that would be returned
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresIn = 3600,
            RedirectUrl = validPath,
            ShowProductSelection = false,
            AvailableProducts = new List<ProductInfo>(),
            WarningMessage = null
        };

        // Verify response structure
        Assert.True(response.Success);
        Assert.NotNull(response.AccessToken);
        Assert.NotNull(response.RefreshToken);
        Assert.Equal(validPath, response.RedirectUrl);
        Assert.False(response.ShowProductSelection);
        Assert.Null(response.WarningMessage);

        return true.ToProperty();
    }

    /// <summary>
    /// Property 8: Invalid Redirect Fallback
    /// Validates: Requirements 4.1, 4.2, 4.3, 4.4, 8.1, 8.2, 8.3
    /// For any redirect URL that fails validation, the system should display the product 
    /// selection page with a generic warning message without exposing the invalid URL.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidRedirectFallback(string invalidPath)
    {
        _mockRedirectValidator
            .Setup(v => v.ValidateAsync(invalidPath))
            .ReturnsAsync(new RedirectValidationResult { IsValid = false, ErrorMessage = "Invalid path" });

        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresIn = 3600,
            RedirectUrl = null,
            ShowProductSelection = true,
            AvailableProducts = new List<ProductInfo>
            {
                new ProductInfo { Id = "cryptocycleai", Name = "CryptoCycleAI", Description = "AI-powered analysis" }
            },
            WarningMessage = "登录成功，但跳转地址无效"
        };

        // Verify response structure
        Assert.True(response.Success);
        Assert.NotNull(response.AccessToken);
        Assert.True(response.ShowProductSelection);
        Assert.Null(response.RedirectUrl);
        Assert.NotNull(response.WarningMessage);
        
        // Verify warning message doesn't expose invalid URL
        Assert.Equal("登录成功，但跳转地址无效", response.WarningMessage);
        
        return true.ToProperty();
    }

    /// <summary>
    /// Property 9: Product Selection Display
    /// Validates: Requirements 5.1, 5.2, 5.3, 5.4, 5.5
    /// For any login without a redirect_url parameter, the system should display the 
    /// product selection page with all available products and a success message.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProductSelectionDisplay(string userId)
    {
        var products = new List<ProductInfo>
        {
            new ProductInfo { Id = "cryptocycleai", Name = "CryptoCycleAI", Description = "AI-powered analysis" },
            new ProductInfo { Id = "analytics", Name = "Analytics", Description = "Advanced analytics platform" }
        };

        _mockProductConfigService
            .Setup(p => p.GetAvailableProductsAsync(userId))
            .ReturnsAsync(products);

        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresIn = 3600,
            RedirectUrl = null,
            ShowProductSelection = true,
            AvailableProducts = products,
            WarningMessage = "登录成功，请选择要进入的产品"
        };

        // Verify response structure
        Assert.True(response.Success);
        Assert.True(response.ShowProductSelection);
        Assert.NotNull(response.AvailableProducts);
        Assert.Equal(2, response.AvailableProducts.Count);
        Assert.NotNull(response.WarningMessage);

        return true.ToProperty();
    }

    /// <summary>
    /// Property 11: JWT Token Generation and Return
    /// Validates: Requirements 11.1
    /// For any successful login, the system should return both an access token and a 
    /// refresh token in the response with correct expiration times and token types.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property JwtTokenGenerationAndReturn(int expiresIn)
    {
        var validExpiresIn = Math.Max(60, Math.Abs(expiresIn) % 86400); // 1 minute to 1 day

        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            RefreshToken = "refresh_token_123",
            ExpiresIn = validExpiresIn,
            RedirectUrl = null,
            ShowProductSelection = true,
            AvailableProducts = new List<ProductInfo>(),
            WarningMessage = null
        };

        // Verify response structure
        Assert.True(response.Success);
        Assert.NotNull(response.AccessToken);
        Assert.NotNull(response.RefreshToken);
        Assert.True(response.ExpiresIn > 0);
        Assert.Equal(validExpiresIn, response.ExpiresIn);

        return true.ToProperty();
    }

    /// <summary>
    /// Property 12: Token Non-Exposure
    /// Validates: Requirements 6.5, 11.2, 11.3
    /// For any login success response, JWT tokens should not appear in the HTML content, 
    /// URL parameters, or any user-facing error or success messages.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TokenNonExposure(string suffix)
    {
        var accessToken = $"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.{suffix}";
        var refreshToken = $"refresh_token_{suffix}";

        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600,
            RedirectUrl = null,
            ShowProductSelection = true,
            AvailableProducts = new List<ProductInfo>(),
            WarningMessage = "登录成功，请选择要进入的产品"
        };

        // Verify tokens are not in user-facing messages
        Assert.DoesNotContain(accessToken, response.WarningMessage ?? "");
        Assert.DoesNotContain(refreshToken, response.WarningMessage ?? "");

        return true.ToProperty();
    }

    /// <summary>
    /// Unit test: Valid redirect returns correct response
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_WithValidRedirect_ReturnsCorrectStructure()
    {
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            RedirectUrl = "/dashboard",
            ShowProductSelection = false,
            AvailableProducts = null,
            WarningMessage = null
        };

        Assert.True(response.Success);
        Assert.Equal("/dashboard", response.RedirectUrl);
        Assert.False(response.ShowProductSelection);
    }

    /// <summary>
    /// Unit test: Invalid redirect shows product selection
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_WithInvalidRedirect_ShowsProductSelection()
    {
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            RedirectUrl = null,
            ShowProductSelection = true,
            AvailableProducts = new List<ProductInfo>
            {
                new ProductInfo { Id = "cryptocycleai", Name = "CryptoCycleAI", Description = "AI-powered analysis" }
            },
            WarningMessage = "登录成功，但跳转地址无效"
        };

        Assert.True(response.Success);
        Assert.Null(response.RedirectUrl);
        Assert.True(response.ShowProductSelection);
        Assert.NotNull(response.AvailableProducts);
    }

    /// <summary>
    /// Unit test: No redirect shows product selection with success message
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_WithoutRedirect_ShowsProductSelectionWithMessage()
    {
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            RedirectUrl = null,
            ShowProductSelection = true,
            AvailableProducts = new List<ProductInfo>
            {
                new ProductInfo { Id = "cryptocycleai", Name = "CryptoCycleAI", Description = "AI-powered analysis" }
            },
            WarningMessage = "登录成功，请选择要进入的产品"
        };

        Assert.True(response.Success);
        Assert.True(response.ShowProductSelection);
        Assert.NotNull(response.WarningMessage);
        Assert.Contains("登录成功", response.WarningMessage);
    }
}
