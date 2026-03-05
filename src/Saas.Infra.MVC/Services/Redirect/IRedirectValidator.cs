namespace Saas.Infra.MVC.Services.Redirect;

/// <summary>
/// Validates redirect URLs against security rules and whitelist
/// </summary>
public interface IRedirectValidator
{
    /// <summary>
    /// Validates a redirect URL against security rules and whitelist
    /// </summary>
    /// <param name="redirectUrl">The URL to validate (relative path only)</param>
    /// <returns>Validation result with success flag and error message if invalid</returns>
    Task<RedirectValidationResult> ValidateAsync(string? redirectUrl);
}

/// <summary>
/// Result of redirect URL validation
/// </summary>
public class RedirectValidationResult
{
    /// <summary>
    /// Whether the redirect URL is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if validation failed (not exposed to users)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The validated path if valid
    /// </summary>
    public string? ValidatedPath { get; set; }

    /// <summary>
    /// Timestamp of validation
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}
