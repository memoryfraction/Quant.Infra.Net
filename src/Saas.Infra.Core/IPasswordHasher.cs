using System;
using System.Collections.Generic;
using System.Text;

namespace Saas.Infra.Core
{
    // 位置：Saas.Infra.Core/IPasswordHasher.cs
    public interface IPasswordHasher
    {
        // 生成哈希（用于注册）
        string HashPassword(string password);
        // 验证哈希（用于登录）
        bool VerifyPassword(string hashedPassword, string providedPassword);
    }
}
