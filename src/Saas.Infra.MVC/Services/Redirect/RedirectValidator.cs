using Microsoft.Extensions.Logging;

namespace Saas.Infra.MVC.Services.Redirect;

/// <summary>
/// Validates redirect URLs against security rules and whitelist
/// </summary>
public class RedirectValidator : IRedirectValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RedirectValidator> _logger;
    private readonly List<string> _whitelist;

    public RedirectValidator(IConfiguration configuration, ILogger<RedirectValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _whitelist = _configuration.GetSection("Products:Whitelist")
            .Get<List<string>>() ?? new List<string>();
    }

    /// <summary>
    /// Validates a redirect URL against security rules and whitelist
    /// </summary>
    public async Task<RedirectValidationResult> ValidateAsync(string? redirectUrl)
    {
        return await Task.FromResult(Validate(redirectUrl));
    }

    private RedirectValidationResult Validate(string? redirectUrl)
    {
        // 1. Check null/empty/whitespace
        if (string.IsNullOrWhiteSpace(redirectUrl))
        {
            return new RedirectValidationResult { IsValid = true, ValidatedPath = null };
        }

        // 2. Decode URL (handle URL encoding)
        string decodedUrl;
        try
        {
            decodedUrl = Uri.UnescapeDataString(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Redirect validation failed: malformed URL - {ErrorMessage}", ex.Message);
            return new RedirectValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Malformed URL" 
            };
        }

        // 3. Check for protocol schemes (http://, https://, ftp://, javascript:, data:, file://, etc.)
        if (ContainsProtocolScheme(decodedUrl))
        {
            _logger.LogWarning("Redirect validation failed: external protocol detected in URL");
            return new RedirectValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Protocol scheme not allowed" 
            };
        }

        // 4. Check for path traversal attempts (../, ..\)
        if (decodedUrl.Contains("../") || decodedUrl.Contains("..\\"))
        {
            _logger.LogWarning("Redirect validation failed: path traversal attempt detected");
            return new RedirectValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Path traversal not allowed" 
            };
        }

        // 5. Check for relative path requirement (must start with /)
        if (!decodedUrl.StartsWith("/"))
        {
            _logger.LogWarning("Redirect validation failed: not a relative path");
            return new RedirectValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Must be a relative path" 
            };
        }

        // 6. Check against configured whitelist
        if (!_whitelist.Contains(decodedUrl))
        {
            _logger.LogWarning("Redirect validation failed: path not in whitelist");
            return new RedirectValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Path not in whitelist" 
            };
        }

        return new RedirectValidationResult 
        { 
            IsValid = true, 
            ValidatedPath = decodedUrl 
        };
    }

    /// <summary>
    /// Checks if URL contains protocol schemes
    /// </summary>
    private bool ContainsProtocolScheme(string url)
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

        return schemes.Any(s => url.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }
}
