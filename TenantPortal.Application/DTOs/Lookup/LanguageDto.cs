namespace TenantPortal.Application.DTOs.Lookup
{
    public sealed class LanguageDto
    {
        public long LanguageId { get; set; }
        public string LanguageCode { get; set; } = default!;
        public string LanguageName { get; set; } = default!;
        public bool IsRTL { get; set; }
        public bool SelectByDefault { get; set; }
    }
}
