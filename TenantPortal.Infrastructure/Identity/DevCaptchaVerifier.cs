using Microsoft.Extensions.Configuration;
using TenantPortal.Application.Interfaces.Identity;

namespace TenantPortal.Infrastructure.Identity
{
    // Development CAPTCHA verifier — only the configured token is treated
    // as valid (default "x"). Mirrors ClyiqERP's AllowAllCaptchaVerifier;
    // swap for a real provider (hCaptcha/reCAPTCHA) when one is wired up.
    public sealed class DevCaptchaVerifier : ICaptchaVerifier
    {
        private readonly string _validDevToken;

        public DevCaptchaVerifier(IConfiguration configuration)
        {
            _validDevToken = configuration["Captcha:DevValidToken"]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_validDevToken))
                _validDevToken = "x";
        }

        public Task<bool> VerifyAsync(string token, string? ip, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token))
                return Task.FromResult(false);

            var isValid = string.Equals(token.Trim(), _validDevToken, StringComparison.Ordinal);
            return Task.FromResult(isValid);
        }
    }
}
