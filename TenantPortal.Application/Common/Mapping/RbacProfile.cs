using AutoMapper;
using TenantPortal.Application.DTOs.Rbac;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.Application.Common.Mappings
{
    public class RbacProfile : Profile
    {
        public RbacProfile()
        {
            CreateMap<SysRole, RoleDto>();
        }
    }
}