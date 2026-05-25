using TenantPortal.Application.Interfaces.Identity;

namespace TenantPortal.Infrastructure.Identity;


public class TenantContext : ITenantContext
{

    public Guid? TenantId { get; }
    public bool IsPlatformUser => !TenantId.HasValue;

    public TenantContext(Guid? tenantId) { TenantId = tenantId; }

}