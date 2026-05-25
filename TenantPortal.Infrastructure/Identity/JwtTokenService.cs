using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TenantPortal.Application.Interfaces.Identity;

namespace TenantPortal.Infrastructure.Identity
{
    public class JwtTokenService : ITokenService
    {
        private readonly string _issuer, _audience, _key;
        private readonly int _minutes;

        public JwtTokenService(string issuer, string audience, string key, int expiryMinutes)
        { _issuer = issuer; _audience = audience; _key = key; _minutes = expiryMinutes; }


        public string IssueToken(
                Guid userId,
                string email,
                Guid? tenantId,
                string role,
                bool isGlobal,
                IEnumerable<string> countries,
                out Guid jti)
        {
            jti = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var claims = new List<Claim>
        {
            new("jti", jti.ToString()),
            new(JwtRegisteredClaimNames.Iss, _issuer),
            new(JwtRegisteredClaimNames.Iat, ((DateTimeOffset)now).ToUnixTimeSeconds().ToString()),
            new("user_id", userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("role", role),
            new("token_type", "access"),
            new("is_global", isGlobal ? "true" : "false")
        };

            if (tenantId.HasValue) claims.Add(new("tenant_id", tenantId.Value.ToString()));


            if (countries is not null)
            {
                foreach (var code in countries)
                {
                    if (!string.IsNullOrWhiteSpace(code))
                        claims.Add(new Claim("country", code.Trim().ToUpperInvariant()));
                }
            }


            var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(_key));
            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(_minutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}