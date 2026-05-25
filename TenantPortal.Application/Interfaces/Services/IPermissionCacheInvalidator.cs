namespace TenantPortal.Application.Interfaces.Services
{
    public interface IPermissionCacheInvalidator
    {
        void InvalidateRole(int roleId);
        void InvalidateUser(Guid userId);
        void InvalidateAll();
    }
}
