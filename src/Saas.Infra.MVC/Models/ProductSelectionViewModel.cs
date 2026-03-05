using Saas.Infra.MVC.Services.Product;

namespace Saas.Infra.MVC.Models;

/// <summary>
/// View model for product selection page
/// </summary>
public class ProductSelectionViewModel
{
    /// <summary>
    /// Success message to display
    /// </summary>
    public string SuccessMessage { get; set; } = "Login succeed.";

    /// <summary>
    /// Optional warning message (for invalid redirect case)
    /// </summary>
    public string? WarningMessage { get; set; }

    /// <summary>
    /// List of available products
    /// </summary>
    public List<ProductInfo> Products { get; set; } = new();

    /// <summary>
    /// JWT access token (for client-side use)
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// JWT refresh token (for client-side use)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Access token expiration time in seconds
    /// </summary>
    public int ExpiresIn { get; set; }
}
