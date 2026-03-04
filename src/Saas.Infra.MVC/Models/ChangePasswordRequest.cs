using System.ComponentModel.DataAnnotations;

namespace Saas.Infra.MVC.Models
{
    /// <summary>
    /// 请求：修改密码。
    /// Request model for changing password.
    /// </summary>
    public class ChangePasswordRequest
    {
        [Required]
        public string OldPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
