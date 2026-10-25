namespace TenantPortal.Domain.Entities.Rbac
{
    // These are seeded by SeedCoreData with auto-incrementing IDs, so these
    // constants are only valid against a database seeded by it from empty —
    // they're not a guaranteed stable mapping otherwise. Only RoleIds actually
    // read anywhere in code are listed; resolve others by slug instead
    // (IAuthorizationRepository.ResolveRoleIdBySlugAsync).
    public static class WellKnownRoles
    {
        public const int Owner = 1;
        public const int EnvironmentManager = 5;
        public const int TenantUser = 6;
    }
}
