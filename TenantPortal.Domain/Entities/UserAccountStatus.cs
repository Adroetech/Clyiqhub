namespace TenantPortal.Domain.Entities
{
    /// <summary>
    /// Single lifecycle state for a SysUserInfo account — replaces the old pair of an
    /// independent "Status" string (Active/Pending/Pending Approval/Rejected) plus a
    /// separate "IsActive" bool. Disabled covers what IsActive=false used to mean
    /// (an already-approved account turned off by an admin).
    /// </summary>
    public enum UserAccountStatus
    {
        Pending = 0,
        PendingApproval = 1,
        Rejected = 2,
        Active = 3,
        Disabled = 4
    }
}
