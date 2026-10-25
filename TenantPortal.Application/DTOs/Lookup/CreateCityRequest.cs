using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Application.DTOs.Lookup
{
    public sealed class CreateCityRequest
    {
        [Required]
        public Guid CountryId { get; set; }

        [Required, StringLength(100, MinimumLength = 2)]
        public string CityName { get; set; } = default!;
    }
}
