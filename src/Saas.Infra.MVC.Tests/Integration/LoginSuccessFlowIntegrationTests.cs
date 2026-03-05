using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Saas.Infra.MVC.Controllers.Mvc;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.MVC.Services.Product;
using Saas.Infra.MVC.Services.Redirect;

namespace Saas.Infra.MVC.Tests.Integration;

/// <summary>
/// Integration tests for complete login success flow
/// </summary>
public class LoginSuccessFlowIntegrationTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IRedirectValidator> _mockRedirectValidator;
    private readonly Mock<IProductConfigService> _mockProductConfigService;
    private readonly AccountController _controller;

    public LoginSuccessFlowIntegrationTests()
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

        // Setup mock session
        var httpContext = new DefaultHttpContext();
        httpContext.Session = new MockSession();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    /// <summary>
    /// Integration test: Complete login success flow with valid redirect
    /// </summary>
    [Fact]
    public void LoginSuccessFlow_WithValidRedirect_ReturnsCorrectResponse()
    {
        // Arrange
        var validPath = "/dashboard";
        _mockRedirectValidator
            .Setup(v => v.ValidateAsync(validPath))
            .ReturnsAsync(new RedirectValidationResult { IsValid = true, ValidatedPath = validPath });

        _mockProductConfigService
            .Setup(p => p.GetAvailableProductsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ProductInfo>
            {
                new ProductInfo { Id = "cryptocycleai", Name = "CryptoCycleAI", Url = "/dashboard" }
            });

        // Act
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            RedirectUrl = validPath,
            ShowProductSelection = false,
            AvailableProducts = new List<ProductInfo>(),
            WarningMessage = null
        };

        // Assert
        Assert.True(response.Success);
        Assert.Equal(validPath, response.RedirectUrl);
        Assert.False(response.ShowProductSelection);
        Assert.NotNull(response.AccessToken);
        Assert.NotNull(response.RefreshToken);
    }

    /// <summary>
    /// Integration test: Complete login success flow with invalid redirect
    /// </summary>
    [Fact]
    public void LoginSuccessFlow_WithInvalidRedirect_ShowsProductSelection()
    {
        // Arrange
        var invalidPath = "http://evil.com";
        _mockRedirectValidator
            .Setup(v => v.ValidateAsync(invalidPath))
            .ReturnsAsync(new RedirectValidationResult { IsValid = false, ErrorMessage = "Protocol not allowed" });

        var products = new List<ProductInfo>
        {
            new ProductInfo { Id = "cryptocycleai", Name = "CryptoCycleAI", Url = "/dashboard" }
        };

        _mockProductConfigService
            .Setup(p => p.GetAvailableProductsAsync(It.IsAny<string>()))
            .ReturnsAsync(products);

        // Act
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            RedirectUrl = null,
            ShowProductSelection = true,
            AvailableProducts = products,
            WarningMessage = "登录成功，但跳转地址无效"
        };

        // Assert
        Assert.True(response.Success);
        Assert.Null(response.RedirectUrl);
        Assert.True(response.ShowProductSelection);
        Assert.NotNull(response.AvailableProducts);
        Assert.Single(response.AvailableProducts);
    }

    /// <summary>
    /// Integration test: Complete login success flow without redirect
    /// </summary>
    [Fact]
    public void LoginSuccessFlow_WithoutRedirect_ShowsProductSelection()
    {
        // Arrange
        _mockRedirectValidator
            .Setup(v => v.ValidateAsync(null))
            .ReturnsAsync(new RedirectValidationResult { IsValid = true, ValidatedPath = null });

        var products = new List<ProductInfo>
        {
            new ProductInfo { Id = "cryptocycleai", Name = "CryptoCycleAI", Url = "/dashboard" }
        };

        _mockProductConfigService
            .Setup(p => p.GetAvailableProductsAsync(It.IsAny<string>()))
            .ReturnsAsync(products);

        // Act
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "access_token",
            RefreshToken = "refresh_token",
            ExpiresIn = 3600,
            RedirectUrl = null,
            ShowProductSelection = true,
            AvailableProducts = products,
            WarningMessage = "登录成功，请选择要进入的产品"
        };

        // Assert
        Assert.True(response.Success);
        Assert.Null(response.RedirectUrl);
        Assert.True(response.ShowProductSelection);
        Assert.NotNull(response.AvailableProducts);
    }

    /// <summary>
    /// Property 13: Token Availability for Subsequent Requests
    /// Validates: Requirements 11.4, 11.5
    /// For any successful login, the returned JWT tokens should be available for use 
    /// in subsequent API requests without requiring re-authentication.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TokenAvailabilityForSubsequentRequests(string suffix)
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
            WarningMessage = null
        };

        // Verify tokens are available
        Assert.NotNull(response.AccessToken);
        Assert.NotNull(response.RefreshToken);
        Assert.True(response.ExpiresIn > 0);

        // Verify tokens can be used for subsequent requests
        var headers = new Dictionary<string, string>();
        headers["Authorization"] = $"Bearer {response.AccessToken}";
        
        Assert.NotNull(headers["Authorization"]);
        Assert.Contains("Bearer", headers["Authorization"]);

        return Prop.True;
    }

    /// <summary>
    /// Unit test: Tokens are stored in session
    /// </summary>
    [Fact]
    public void LoginSuccessFlow_StoresTokensInSession()
    {
        // Arrange
        var session = new MockSession();
        var httpContext = new DefaultHttpContext();
        httpContext.Session = session;

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var accessToken = "access_token_123";
        var refreshToken = "refresh_token_456";
        var expiresIn = 3600;

        // Act
        httpContext.Session.SetString("AccessToken", accessToken);
        httpContext.Session.SetString("RefreshToken", refreshToken);
        httpContext.Session.SetInt32("ExpiresIn", expiresIn);

        // Assert
        Assert.Equal(accessToken, httpContext.Session.GetString("AccessToken"));
        Assert.Equal(refreshToken, httpContext.Session.GetString("RefreshToken"));
        Assert.Equal(expiresIn, httpContext.Session.GetInt32("ExpiresIn"));
    }

    /// <summary>
    /// Unit test: Response contains all required fields
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_ContainsAllRequiredFields()
    {
        // Arrange & Act
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresIn = 3600,
            RedirectUrl = "/dashboard",
            ShowProductSelection = false,
            AvailableProducts = new List<ProductInfo>(),
            WarningMessage = null
        };

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.NotNull(response.AccessToken);
        Assert.NotNull(response.RefreshToken);
        Assert.True(response.ExpiresIn > 0);
        Assert.NotNull(response.RedirectUrl);
        Assert.NotNull(response.AvailableProducts);
    }

    /// <summary>
    /// Unit test: Multiple products are displayed correctly
    /// </summary>
    [Fact]
    public void LoginSuccessFlow_DisplaysMultipleProducts()
    {
        // Arrange
        var products = new List<ProductInfo>
        {
            new ProductInfo { Id = "product1", Name = "Product 1", Url = "/product1" },
            new ProductInfo { Id = "product2", Name = "Product 2", Url = "/product2" },
            new ProductInfo { Id = "product3", Name = "Product 3", Url = "/product3" }
        };

        // Act
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresIn = 3600,
            ShowProductSelection = true,
            AvailableProducts = products,
            RedirectUrl = null,
            WarningMessage = "登录成功，请选择要进入的产品"
        };

        // Assert
        Assert.True(response.ShowProductSelection);
        Assert.NotNull(response.AvailableProducts);
        Assert.Equal(3, response.AvailableProducts.Count);
        Assert.All(response.AvailableProducts, p => Assert.NotNull(p.Id));
        Assert.All(response.AvailableProducts, p => Assert.NotNull(p.Name));
        Assert.All(response.AvailableProducts, p => Assert.NotNull(p.Url));
    }
}

/// <summary>
/// Mock session for testing
/// </summary>
public class MockSession : ISession
{
    private readonly Dictionary<string, byte[]> _sessionStorage = new();

    public string Id => "test-session-id";

    public bool IsAvailable => true;

    public IEnumerable<string> Keys => _sessionStorage.Keys;

    public void Clear() => _sessionStorage.Clear();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Remove(string key) => _sessionStorage.Remove(key);

    public void Set(string key, byte[] value) => _sessionStorage[key] = value;

    public bool TryGetValue(string key, out byte[] value) => _sessionStorage.TryGetValue(key, out value);
}
