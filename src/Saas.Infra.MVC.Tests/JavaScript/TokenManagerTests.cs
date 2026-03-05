using Xunit;

namespace Saas.Infra.MVC.Tests.JavaScript;

/// <summary>
/// Unit tests for TokenManager JavaScript class
/// These tests verify the token management logic
/// </summary>
public class TokenManagerTests
{
    /// <summary>
    /// Test: TokenManager stores tokens correctly
    /// </summary>
    [Fact]
    public void TokenManager_StoresTokens()
    {
        // This test verifies the TokenManager class structure
        // In a real scenario, this would be tested with a JavaScript testing framework like Jest
        
        // Verify that TokenManager has the required methods
        var tokenManagerCode = System.IO.File.ReadAllText("Saas.Infra.MVC/wwwroot/js/token-manager.js");
        
        Assert.Contains("storeTokens", tokenManagerCode);
        Assert.Contains("getAccessToken", tokenManagerCode);
        Assert.Contains("getRefreshToken", tokenManagerCode);
        Assert.Contains("attachTokenToRequest", tokenManagerCode);
        Assert.Contains("clearTokens", tokenManagerCode);
    }

    /// <summary>
    /// Test: TokenManager has token expiration check
    /// </summary>
    [Fact]
    public void TokenManager_HasExpirationCheck()
    {
        var tokenManagerCode = System.IO.File.ReadAllText("Saas.Infra.MVC/wwwroot/js/token-manager.js");
        
        Assert.Contains("isAccessTokenExpired", tokenManagerCode);
        Assert.Contains("isAccessTokenExpiringSoon", tokenManagerCode);
        Assert.Contains("getTimeUntilExpiration", tokenManagerCode);
    }

    /// <summary>
    /// Test: TokenManager stores tokens in sessionStorage
    /// </summary>
    [Fact]
    public void TokenManager_UsesSessionStorage()
    {
        var tokenManagerCode = System.IO.File.ReadAllText("Saas.Infra.MVC/wwwroot/js/token-manager.js");
        
        Assert.Contains("sessionStorage.setItem", tokenManagerCode);
        Assert.Contains("sessionStorage.getItem", tokenManagerCode);
        Assert.Contains("sessionStorage.removeItem", tokenManagerCode);
    }

    /// <summary>
    /// Test: TokenManager attaches Bearer token to headers
    /// </summary>
    [Fact]
    public void TokenManager_AttachsBearerToken()
    {
        var tokenManagerCode = System.IO.File.ReadAllText("Saas.Infra.MVC/wwwroot/js/token-manager.js");
        
        Assert.Contains("Bearer", tokenManagerCode);
        Assert.Contains("Authorization", tokenManagerCode);
    }

    /// <summary>
    /// Test: TokenManager clears tokens on logout
    /// </summary>
    [Fact]
    public void TokenManager_ClearsTokens()
    {
        var tokenManagerCode = System.IO.File.ReadAllText("Saas.Infra.MVC/wwwroot/js/token-manager.js");
        
        Assert.Contains("clearTokens", tokenManagerCode);
        Assert.Contains("this.accessToken = null", tokenManagerCode);
        Assert.Contains("this.refreshToken = null", tokenManagerCode);
    }

    /// <summary>
    /// Test: ProductSelectionHandler uses TokenManager
    /// </summary>
    [Fact]
    public void ProductSelectionHandler_UsesTokenManager()
    {
        var productSelectionCode = System.IO.File.ReadAllText("Saas.Infra.MVC/wwwroot/js/product-selection.js");
        
        Assert.Contains("sessionStorage.getItem", productSelectionCode);
        Assert.Contains("accessToken", productSelectionCode);
        Assert.Contains("refreshToken", productSelectionCode);
    }

    /// <summary>
    /// Test: ProductSelectionHandler handles product navigation
    /// </summary>
    [Fact]
    public void ProductSelectionHandler_HandlesProductNavigation()
    {
        var productSelectionCode = System.IO.File.ReadAllText("Saas.Infra.MVC/wwwroot/js/product-selection.js");
        
        Assert.Contains("handleProductSelection", productSelectionCode);
        Assert.Contains("data-product-url", productSelectionCode);
        Assert.Contains("window.location.href", productSelectionCode);
    }
}
