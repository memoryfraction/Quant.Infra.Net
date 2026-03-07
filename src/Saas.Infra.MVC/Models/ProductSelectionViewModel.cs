using Saas.Infra.MVC.Services.Product;

namespace Saas.Infra.MVC.Models;

public class ProductSelectionViewModel
{
    public string SuccessMessage { get; set; } = "Login succeed.";
    public string? WarningMessage { get; set; }
    public List<ProductInfo> Products { get; set; } = new();
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
}
