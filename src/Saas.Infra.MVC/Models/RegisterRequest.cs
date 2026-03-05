using System.ComponentModel.DataAnnotations;

namespace Saas.Infra.MVC.Models
{
    /// <summary>
    /// 表示用户注册请求的数据传输对象。 / Represents the data transfer object for a user registration request.
    /// </summary>
    public class RegisterRequest
    {
        /// <summary>
        /// 获取或设置电子邮件地址。 / Gets or sets the email address.
        /// </summary>
        /// <value>用户的电子邮件地址。 / The user's email address.</value>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置用户名（可选）。 / Gets or sets the username (optional).
        /// </summary>
        /// <value>用户的用户名。如果未提供，系统将自动生成。 / The user's username. If not provided, the system will auto-generate one.</value>
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters")]
        public string? Username { get; set; }

        /// <summary>
        /// 获取或设置密码。 / Gets or sets the password.
        /// </summary>
        /// <value>用户的登录密码。 / The password used for login.</value>
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置客户端标识符。 / Gets or sets the client identifier.
        /// </summary>
        /// <value>可选的客户端ID，用于标识请求来源。 / Optional client ID used to identify the request source.</value>
        public string? ClientId { get; set; }
    }
}
