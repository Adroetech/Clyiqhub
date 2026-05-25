using Azure.Core;
using System.Text;
using TenantPortal.Application.Interfaces.Identity;

namespace TenantPortal.Infrastructure.Identity
{
    public class BcryptPasswordHasher : IPasswordHasher
    {
        public byte[] Hash(string password)
            => Encoding.UTF8.GetBytes(BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12));

        public bool Verify(string password, byte[] stored)
        {
            if (stored is null || stored.Length == 0) return false;
            var hash = Encoding.UTF8.GetString(stored); 
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
    }
}