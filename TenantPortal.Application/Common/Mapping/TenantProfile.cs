using AutoMapper;
using TenantPortal.Application.DTOs.Tenant;
using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Common.Mappings
{
    public class TenantProfile : Profile
    {
        public TenantProfile()
        {
            CreateMap<TenantCreateRequest, Tenant>()
                .ForMember(d => d.TenantId, opt => opt.Ignore())
                .ForMember(d => d.Status, opt => opt.Ignore())
                .ForMember(d => d.CreatedAt, opt => opt.Ignore())
                .ForMember(d => d.ApprovedAt, opt => opt.Ignore())
                .ForMember(d => d.ApprovedBy, opt => opt.Ignore());
        }
    }
}