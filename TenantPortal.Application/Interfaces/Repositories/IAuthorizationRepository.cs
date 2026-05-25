    //namespace TenantPortal.Application.Interfaces
    //{
    //    public interface IAuthorizationRepository
    //    {
    //        // role resolution (user.Role slug → SysRoles.RoleId)
    //        Task<int?> ResolveRoleIdBySlugAsync(string roleSlug, CancellationToken ct);

    //        // process/verb grants for a role (from SysRolePermission → SysProcessPermission → SysPermissionss)

    //        // scoping
    //        Task<HashSet<Guid>> GetUserCountriesAsync(Guid userId, int roleId, CancellationToken ct);
    //        Task<HashSet<Guid>> GetUserTenantsAsync(Guid userId, int roleId, CancellationToken ct);
    //    }
    //}


    namespace TenantPortal.Application.Interfaces
    {
        public interface IAuthorizationRepository
        {
            Task<int?> ResolveRoleIdBySlugAsync(string roleSlug, CancellationToken ct);
            Task<HashSet<(string process, string verb)>> GetRoleGrantsAsync(int roleId, CancellationToken ct);
            Task<HashSet<Guid>> GetUserCountriesAsync(Guid userId, int roleId, CancellationToken ct);
            Task<HashSet<Guid>> GetUserTenantsAsync(Guid userId, int roleId, CancellationToken ct);
            Task<HashSet<string>> GetUserCountryCodesByRoleAsync(Guid userId, int roleId, CancellationToken ct);



            Task<IReadOnlyList<int>> GetUserRoleIdsAsync(Guid userId, CancellationToken ct);

            Task<HashSet<string>> GetRolePermissionCodesAsync(int roleId, CancellationToken ct);
            // returns codes like module.process.action

            Task<string?> ResolveRoleSlugByIdAsync(int roleId, CancellationToken ct);


        }
    }
