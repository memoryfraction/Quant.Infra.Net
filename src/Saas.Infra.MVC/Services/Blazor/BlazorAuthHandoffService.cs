using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Saas.Infra.MVC.Services.Blazor
{
    /// <summary>
    /// Singleton service that creates and consumes single-use auth-handoff codes.
    /// Used to securely transfer a JWT token from the Blazor Server circuit to a plain
    /// HTTP response, so the browser receives the <c>Set-Cookie</c> header.
    /// </summary>
    public sealed class BlazorAuthHandoffService
    {
        private readonly ConcurrentDictionary<string, (string Token, DateTime Expiry)> _pending = new();

        /// <summary>
        /// Creates a one-time URL-safe handoff code for the given JWT token.
        /// The code expires after 60 seconds.
        /// </summary>
        /// <param name="token">The JWT access token to hand off.</param>
        /// <returns>A URL-safe Base64 one-time code.</returns>
        /// <exception cref="ArgumentNullException">Thrown when token is null or whitespace.</exception>
        public string CreateHandoff(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token));

            var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                               .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            _pending[code] = (token, DateTime.UtcNow.AddMinutes(1));
            return code;
        }

        /// <summary>
        /// Consumes a one-time handoff code and returns the associated token.
        /// Returns <c>null</c> if the code is unknown or expired.
        /// </summary>
        /// <param name="code">The one-time code.</param>
        /// <returns>The JWT token, or <c>null</c> if not found / expired.</returns>
        /// <exception cref="ArgumentNullException">Thrown when code is null or whitespace.</exception>
        public string? ConsumeHandoff(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentNullException(nameof(code));

            if (_pending.TryRemove(code, out var entry) && entry.Expiry > DateTime.UtcNow)
                return entry.Token;

            return null;
        }
    }
}
