namespace TenantPortal.Domain.Entities
{
    // Admin Manual (20-10-2026) §2.4 widens this from 3 to 7 states. Active/
    // Expired/Terminated keep their original ordinals (every live row is
    // Status=0/Active) — new values are appended, never inserted in the
    // middle, so existing data never silently reinterprets as the wrong
    // status.
    public enum AgreementStatus
    {
        Active = 0,
        Expired = 1,
        Terminated = 2,
        Draft = 3,
        PendingApproval = 4,
        Approved = 5,
        Suspended = 6
    }
}
