using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Application.DTOs.Rbac
{
    public sealed class RoleDto
    {
        public int RoleId { get; set; }
        public string Name { get; set; } = default!;
        public string Slug { get; set; } = default!;
        public string Level { get; set; } = default!;
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
        [RegularExpression("^(Platform|Tenant)$", ErrorMessage = "Level must be either 'Platform' or 'Tenant'.")]
        public string Level { get; set; } = "Platform";

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


    public sealed class UpdateResultDto
    {
        public bool Changed { get; set; }
        public string Message { get; set; } = "";
    }


}