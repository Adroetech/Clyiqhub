using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Application.DTOs.Lookup
{
    public sealed class CreateCountryRequest
    {
        [Required, StringLength(10, MinimumLength = 2)]
        public string CountryCode { get; set; } = default!;

        [Required, StringLength(150, MinimumLength = 2)]
        public string Name { get; set; } = default!;
    }
}
