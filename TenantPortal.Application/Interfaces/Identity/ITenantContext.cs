namespace TenantPortal.Application.Interfaces.Identity;
public interface ITenantContext
{
    Guid? TenantId { get; }
    bool IsPlatformUser { get; }
}