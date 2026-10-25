namespace TenantPortal.Domain.Entities
{
    /// <summary>
    /// Scopes a (user, role) pair to a set of countries — e.g. a Subscription Manager/Agent
    /// can only act on tenants in the countries listed here for that role. Composite key
    /// (UserId, Role, CountryCode) matches the real table exactly; Role is the role slug
    /// (e.g. "subscription_manager"), not a RoleId FK — there is no surrogate key column.
    /// </summary>
    public class SysUserRoleCountry
    {
        public Guid UserId { get; set; }
        public string Role { get; set; } = default!;
        public string CountryCode { get; set; } = default!;
    }
}
