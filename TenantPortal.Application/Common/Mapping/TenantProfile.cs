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
                .ForMember(d => d.CityId, opt => opt.MapFrom(s => s.City))
                .ForMember(d => d.City, opt => opt.Ignore())
                .ForMember(d => d.Country, opt => opt.Ignore())
                .ForMember(d => d.Language, opt => opt.Ignore());
                // BackupPlan maps automatically — both sides are the same enum type
                // (TenantBackupPlan), no parsing needed.
        }
    }
}