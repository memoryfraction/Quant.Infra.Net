using System.ComponentModel.DataAnnotations;

namespace Saas.Infra.MVC.Models
{
    /// <summary>
    /// 表示用户登录请求的数据传输对象。 / Represents the data transfer object for a user login request.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 获取或设置用户名。 / Gets or sets the username.
        /// </summary>
        /// <value>用户的登录名称。 / The username used for login.</value>
        [Required(ErrorMessage = "用户名不能为空")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "用户名长度必须在3到100个字符之间")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置密码。 / Gets or sets the password.
        /// </summary>
        /// <value>用户的登录密码。 / The password used for login.</value>
        [Required(ErrorMessage = "密码不能为空")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度必须在6到100个字符之间")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置客户端标识符。 / Gets or sets the client identifier.
        /// </summary>
        /// <value>可选的客户端ID，用于标识请求来源。 / Optional client ID used to identify the request source.</value>
        public string? ClientId { get; set; }
    }
}
