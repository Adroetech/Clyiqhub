using System.Security.Cryptography;
using System.Text;

namespace TenantPortal.Application.Common
{
    public static class TenantCodeGenerator
    {
        private static readonly char[] Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        public static string Generate(int length)
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
                sb.Append(Alphabet[bytes[i] % Alphabet.Length]);
            return sb.ToString();
        }
    }
}