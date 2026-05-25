using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace TenantPortal.Infrastructure.Services
{
    public interface ISecretProtector
    {
        byte[] Protect(string plain);
        string Unprotect(byte[] cipher);
    }

    public class SecretProtector : ISecretProtector
    {
        private readonly IDataProtector _p;

        public SecretProtector(IDataProtectionProvider dp)
        {
            _p = dp.CreateProtector("EmailSettings.SmtpPassword");
        }

        public byte[] Protect(string plain)
        {
            var protectedText = _p.Protect(plain);          // string
            return Encoding.UTF8.GetBytes(protectedText);   // -> varbinary
        }

        public string Unprotect(byte[] cipher)
        {

            if (cipher == null || cipher.Length < 10)
                throw new InvalidOperationException("SMTP password not configured. Please update Email Settings.");

            var protectedText = Encoding.UTF8.GetString(cipher); // <- varbinary

            return _p.Unprotect(protectedText);
        }
    }
}