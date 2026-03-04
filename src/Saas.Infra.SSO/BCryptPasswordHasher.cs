using Saas.Infra.Core;

namespace Saas.Infra.SSO
{
    /// <summary>
    /// 使用 BCrypt 实现的密码哈希器。
    /// BCrypt-based password hasher implementation.
    /// </summary>
    public class BCryptPasswordHasher : IPasswordHasher
    {
        /// <summary>
        /// 生成密码哈希。
        /// Hash the provided password.
        /// </summary>
        public string HashPassword(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        /// <summary>
        /// 验证明文密码与哈希是否匹配。
        /// Verify that the provided password matches the hashed password.
        /// </summary>
        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            if (hashedPassword == null) throw new ArgumentNullException(nameof(hashedPassword));
            if (providedPassword == null) throw new ArgumentNullException(nameof(providedPassword));
            return BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
        }
    }
}
