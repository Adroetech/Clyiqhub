using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Application.DTOs.Rbac
{
    public sealed class RoleDto
    {
        public int RoleId { get; set; }
        public string Name { get; set; } = default!;
        public string Slug { get; set; } = default!;
        public string RoleType { get; set; } = default!;
        public bool IsActive { get; set; }
    }

    public sealed class PermissionItemDto
    {
        public int ProcessPermissionId { get; set; }
        public string ModuleSlug { get; set; } = default!;
        public string ProcessSlug { get; set; } = default!;
        public string ActionSlug { get; set; } = default!;
        public string Code { get; set; } = default!; // module.process.action
    }

    public sealed class PermissionCatalogDto
    {
        public IReadOnlyList<RoleDto> Roles { get; set; } = Array.Empty<RoleDto>();
        public IReadOnlyList<PermissionItemDto> PermissionItems { get; set; } = Array.Empty<PermissionItemDto>();
    }

    public sealed class PlatformUserDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = default!;
        public string Name { get; set; } = default!;
        public bool IsActive { get; set; }
        public IReadOnlyList<int> RoleIds { get; set; } = Array.Empty<int>();
        public IReadOnlyList<string> RoleNames { get; set; } = Array.Empty<string>();
    }

    public sealed class RolePermissionsDto
    {
        public int RoleId { get; set; }
        public IReadOnlyList<string> PermissionCodes { get; set; } = Array.Empty<string>();
        public IReadOnlyList<int> ProcessPermissionIds { get; set; } = Array.Empty<int>();
    }


    public sealed class CreateRoleRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = default!;

        [Required]
        [StringLength(50, MinimumLength = 2)]
        [RegularExpression("^[a-z0-9_]+$", ErrorMessage = "Slug must be lowercase and contain only a-z, 0-9, and underscore.")]
        public string Slug { get; set; } = default!;

        [Required]
        [RegularExpression("^(SuperAdmin|Admin|TenantAdmin|TenantUser)$", ErrorMessage = "RoleType must be one of: SuperAdmin, Admin, TenantAdmin, TenantUser.")]
        public string RoleType { get; set; } = "TenantUser";

        public bool IsActive { get; set; } = true;

        [Required]
        [MinLength(1, ErrorMessage = "At least one permission must be selected.")]
        public List<int> ProcessPermissionIds { get; set; } = new();
    }

    public sealed class UpdateRoleRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string Name { get; set; } = default!;

        public bool IsActive { get; set; } = true;

        [Required]
        public List<int> ProcessPermissionIds { get; set; } = new();
    }

    public sealed class UpdateUserRolesRequest
    {
        [Required]
        public List<int> RoleIds { get; set; } = new();
    }

    public sealed class CreateUserRequest
    {
        [Required]
        [StringLength(150, MinimumLength = 2)]
        public string Name { get; set; } = default!;

        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = default!;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = default!;

        [Required]
        public int RoleId { get; set; }

        // Optional for every role — only meaningfully scopes permission
        // checks for TenantAdmin (see PermissionAuthorizer), but any role can
        // be tagged with a tenant for reference.
        public Guid? TenantId { get; set; }
    }

    public sealed class CreateUserResultDto
    {
        public Guid UserId { get; set; }
    }


    public sealed class UpdateResultDto
    {
        public bool Changed { get; set; }
        public string Message { get; set; } = "";
    }


}