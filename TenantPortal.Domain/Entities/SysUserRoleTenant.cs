namespace TenantPortal.Domain.Entities
{
    /// <summary>
    /// Scopes a (user, role) pair to a set of specific tenants. Composite key
    /// (UserId, Role, TenantId) matches the real table exactly; Role is the role slug,
    /// not a RoleId FK — there is no surrogate key column.
    /// </summary>
    public class SysUserRoleTenant
    {
        public Guid UserId { get; set; }
        public string Role { get; set; } = default!;
        public Guid TenantId { get; set; }
    }
}
