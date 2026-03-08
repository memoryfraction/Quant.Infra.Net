using System.ComponentModel.DataAnnotations;

namespace Saas.Infra.MVC.Models.Requests
{
    /// <summary>
    /// 后台更新用户请求模型。
    /// Admin update user request model.
    /// </summary>
    public class UpdateManagedUserRequest
    {
        /// <summary>
        /// 用户名。
        /// User name.
        /// </summary>
        [Required(ErrorMessage = "User name is required")]
        [StringLength(50, ErrorMessage = "User name cannot exceed 50 characters")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱。
        /// Email.
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "A valid email is required")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 用户状态。
        /// User status.
        /// </summary>
        [Range(0, 1, ErrorMessage = "Status must be 0 or 1")]
        public short Status { get; set; }
    }
}
