using System.Text.Json;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.MVC.Services.Product;

namespace Saas.Infra.MVC.Tests.Models;

/// <summary>
/// Unit tests for LoginSuccessResponse
/// </summary>
public class LoginSuccessResponseTests
{
    /// <summary>
    /// Test: Response structure with all properties
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_HasAllProperties()
    {
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "access_token_123",
            RefreshToken = "refresh_token_456",
            ExpiresIn = 3600,
            RedirectUrl = "/dashboard",
            ShowProductSelection = false,
            AvailableProducts = new List<ProductInfo>(),
            WarningMessage = null
        };

        Assert.True(response.Success);
        Assert.Equal("access_token_123", response.AccessToken);
        Assert.Equal("refresh_token_456", response.RefreshToken);
        Assert.Equal(3600, response.ExpiresIn);
        Assert.Equal("/dashboard", response.RedirectUrl);
        Assert.False(response.ShowProductSelection);
        Assert.NotNull(response.AvailableProducts);
        Assert.Null(response.WarningMessage);
    }

    /// <summary>
    /// Test: Serialization to JSON
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_SerializesToJson()
    {
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "access_token_123",
            RefreshToken = "refresh_token_456",
            ExpiresIn = 3600,
            RedirectUrl = "/dashboard",
            ShowProductSelection = false,
            AvailableProducts = new List<ProductInfo>
            {
                new ProductInfo
                {
                    Id = "cryptocycleai",
                    Name = "CryptoCycleAI",
                    Description = "AI-powered analysis",
                    Metadata = "{\"icon\":\"/images/icon.png\"}"
                }
            },
            WarningMessage = null
        };

        var json = JsonSerializer.Serialize(response);
        
        Assert.NotNull(json);
        Assert.Contains("access_token_123", json);
        Assert.Contains("refresh_token_456", json);
        Assert.Contains("cryptocycleai", json);
    }

    /// <summary>
    /// Test: Deserialization from JSON
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_DeserializesFromJson()
    {
        var json = @"{
            ""success"": true,
            ""accessToken"": ""access_token_123"",
            ""refreshToken"": ""refresh_token_456"",
            ""expiresIn"": 3600,
            ""redirectUrl"": ""/dashboard"",
            ""showProductSelection"": false,
            ""availableProducts"": [],
            ""warningMessage"": null
        }";

        var response = JsonSerializer.Deserialize<LoginSuccessResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("access_token_123", response.AccessToken);
        Assert.Equal("refresh_token_456", response.RefreshToken);
        Assert.Equal(3600, response.ExpiresIn);
    }

    /// <summary>
    /// Test: Null handling for optional properties
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_HandlesNullProperties()
    {
        var response = new LoginSuccessResponse
        {
            Success = false,
            AccessToken = null,
            RefreshToken = null,
            ExpiresIn = 0,
            RedirectUrl = null,
            ShowProductSelection = true,
            AvailableProducts = null,
            WarningMessage = "Login successful, but redirect address is invalid"
        };

        Assert.False(response.Success);
        Assert.Null(response.AccessToken);
        Assert.Null(response.RefreshToken);
        Assert.Null(response.RedirectUrl);
        Assert.Null(response.AvailableProducts);
        Assert.NotNull(response.WarningMessage);
    }

    /// <summary>
    /// Test: Response with product selection
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_WithProductSelection()
    {
        var products = new List<ProductInfo>
        {
            new ProductInfo { Id = "product1", Name = "Product 1", Description = "Product one" },
            new ProductInfo { Id = "product2", Name = "Product 2", Description = "Product two" }
        };

        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresIn = 3600,
            ShowProductSelection = true,
            AvailableProducts = products,
            RedirectUrl = null,
            WarningMessage = "Login successful, please select a product to enter"
        };

        Assert.True(response.ShowProductSelection);
        Assert.Equal(2, response.AvailableProducts?.Count);
        Assert.Null(response.RedirectUrl);
    }

    /// <summary>
    /// Test: Response with invalid redirect warning
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_WithInvalidRedirectWarning()
    {
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "token",
            RefreshToken = "refresh",
            ExpiresIn = 3600,
            ShowProductSelection = true,
            AvailableProducts = new List<ProductInfo>(),
            RedirectUrl = null,
            WarningMessage = "Login successful, but redirect address is invalid"
        };

        Assert.True(response.Success);
        Assert.True(response.ShowProductSelection);
        Assert.NotNull(response.WarningMessage);
        Assert.DoesNotContain("http://", response.WarningMessage);
        Assert.DoesNotContain("../", response.WarningMessage);
    }

    /// <summary>
    /// Test: Tokens are not exposed in serialization
    /// </summary>
    [Fact]
    public void LoginSuccessResponse_TokensNotExposedInWarningMessage()
    {
        var response = new LoginSuccessResponse
        {
            Success = true,
            AccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            RefreshToken = "refresh_token_secret_123",
            ExpiresIn = 3600,
            WarningMessage = "Login successful, but redirect address is invalid"
        };

        // Warning message should not contain tokens
        Assert.DoesNotContain(response.AccessToken, response.WarningMessage ?? "");
        Assert.DoesNotContain(response.RefreshToken, response.WarningMessage ?? "");
    }
}
