using Saas.Infra.MVC.Services.Product;

namespace Saas.Infra.MVC.Models.Responses;

/// <summary>
/// Response model for successful login
/// </summary>
public class LoginSuccessResponse
{
    /// <summary>
    /// Whether login was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// JWT access token (short-lived, ~15 minutes)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// JWT refresh token (long-lived, ~7 days)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Access token expiration time in seconds
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Redirect URL if validation passed (null if showing product selection)
    /// </summary>
    public string? RedirectUrl { get; set; }

    /// <summary>
    /// Whether to show product selection page
    /// </summary>
    public bool ShowProductSelection { get; set; }

    /// <summary>
    /// Available products for selection
    /// </summary>
    public List<ProductInfo>? AvailableProducts { get; set; }

    /// <summary>
    /// Warning message if redirect was invalid (generic, no URL exposure)
    /// </summary>
    public string? WarningMessage { get; set; }
}
