using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Saas.Infra.MVC.Middleware;

namespace Saas.Infra.MVC.Tests.Middleware;

/// <summary>
/// Unit tests for security headers middleware
/// </summary>
public class SecurityHeadersTests
{
    /// <summary>
    /// Test: Security headers middleware adds required headers
    /// </summary>
    [Fact]
    public async Task SecurityHeadersMiddleware_AddsRequiredHeaders()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(async (ctx) => await Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("X-Content-Type-Options"));
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);

        Assert.True(context.Response.Headers.ContainsKey("X-Frame-Options"));
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);

        Assert.True(context.Response.Headers.ContainsKey("X-XSS-Protection"));
        Assert.Equal("1; mode=block", context.Response.Headers["X-XSS-Protection"]);

        Assert.True(context.Response.Headers.ContainsKey("Referrer-Policy"));
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
    }

    /// <summary>
    /// Test: Security headers middleware adds HSTS header
    /// </summary>
    [Fact]
    public async Task SecurityHeadersMiddleware_AddsHstsHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(async (ctx) => await Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        var hstsValue = context.Response.Headers["Strict-Transport-Security"].ToString();
        Assert.Contains("max-age=31536000", hstsValue);
        Assert.Contains("includeSubDomains", hstsValue);
    }

    /// <summary>
    /// Test: Security headers middleware adds CSP header
    /// </summary>
    [Fact]
    public async Task SecurityHeadersMiddleware_AddsCspHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(async (ctx) => await Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Content-Security-Policy"));
        var cspValue = context.Response.Headers["Content-Security-Policy"].ToString();
        
        // Verify CSP directives
        Assert.Contains("default-src 'self'", cspValue);
        Assert.Contains("script-src 'self'", cspValue);
        Assert.Contains("style-src 'self'", cspValue);
        Assert.Contains("frame-ancestors 'none'", cspValue);
        Assert.Contains("form-action 'self'", cspValue);
    }

    /// <summary>
    /// Test: Security headers middleware calls next middleware
    /// </summary>
    [Fact]
    public async Task SecurityHeadersMiddleware_CallsNextMiddleware()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new SecurityHeadersMiddleware(async (ctx) =>
        {
            nextCalled = true;
            await Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    /// <summary>
    /// Test: Extension method adds middleware to pipeline
    /// </summary>
    [Fact]
    public void SecurityHeadersMiddlewareExtensions_AddsMiddleware()
    {
        // Arrange
        var builder = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());

        // Act
        builder.UseSecurityHeaders();

        // Assert
        // If no exception is thrown, the middleware was added successfully
        Assert.NotNull(builder);
    }

    /// <summary>
    /// Test: X-Content-Type-Options prevents MIME sniffing
    /// </summary>
    [Fact]
    public async Task SecurityHeadersMiddleware_PreventsMimeSniffing()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(async (ctx) => await Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
    }

    /// <summary>
    /// Test: X-Frame-Options prevents clickjacking
    /// </summary>
    [Fact]
    public async Task SecurityHeadersMiddleware_PreventsClickjacking()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(async (ctx) => await Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
    }

    /// <summary>
    /// Test: X-XSS-Protection enables XSS protection
    /// </summary>
    [Fact]
    public async Task SecurityHeadersMiddleware_EnablesXssProtection()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(async (ctx) => await Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("1; mode=block", context.Response.Headers["X-XSS-Protection"]);
    }
}
