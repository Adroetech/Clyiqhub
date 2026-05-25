namespace TenantPortal.Application.Interfaces.Identity
{
    public interface IPasswordHasher
    {
        byte[] Hash(string password);
        bool Verify(string password, byte[] stored);
    }
}