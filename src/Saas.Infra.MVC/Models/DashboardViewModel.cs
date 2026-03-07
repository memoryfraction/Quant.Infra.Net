using Saas.Infra.MVC.Services.Product;

namespace Saas.Infra.MVC.Models
{
    /// <summary>
    /// View model for the dashboard page, includes user profile and available products.
    /// </summary>
    public class DashboardViewModel
    {
        /// <summary>
        /// User ID
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// User email
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Account creation time
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Formatted creation time
        /// </summary>
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        /// <summary>
        /// Avatar initial
        /// </summary>
        public string AvatarInitial => string.IsNullOrEmpty(Username) ? "U" : Username[0].ToString().ToUpper();

        /// <summary>
        /// Available products for the user (displayed on dashboard)
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

        /// <summary>
        /// Optional warning message
        /// </summary>
        public string? WarningMessage { get; set; }
    }
}
