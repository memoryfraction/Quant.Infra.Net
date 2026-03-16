using Saas.Infra.Core;

namespace Saas.Infra.Services.Sso
{
    /// <summary>
    /// 使用 BCrypt 实现的密码哈希器。
    /// BCrypt-based password hasher implementation.
    /// </summary>
    public class BCryptPasswordHasher : IPasswordHasher
    {
        /// <summary>
        /// 生成密码哈希。
        /// Hashes the provided password.
        /// </summary>
        /// <param name="password">明文密码。 / Plain text password.</param>
        /// <returns>密码哈希。 / Password hash.</returns>
        /// <exception cref="ArgumentNullException">当 password 为 null 时抛出。 / Thrown when password is null.</exception>
        public string HashPassword(string password)
        {
            if (password is null)
                throw new ArgumentNullException(nameof(password));

            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        /// <summary>
        /// 验证明文密码与哈希是否匹配。
        /// Verifies that the provided password matches the hashed password.
        /// </summary>
        /// <param name="hashedPassword">已哈希的密码。 / Hashed password.</param>
        /// <param name="providedPassword">待验证的明文密码。 / Provided plain text password.</param>
        /// <returns>匹配返回 true，否则返回 false。 / True if match; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">当参数为 null 时抛出。 / Thrown when arguments are null.</exception>
        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            if (hashedPassword is null)
                throw new ArgumentNullException(nameof(hashedPassword));
            if (providedPassword is null)
                throw new ArgumentNullException(nameof(providedPassword));

            return BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
        }
    }
}
