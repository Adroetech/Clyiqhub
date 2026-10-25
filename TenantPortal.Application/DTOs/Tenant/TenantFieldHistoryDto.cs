namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantFieldHistoryDto
    {
        public string FieldName { get; set; } = default!;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public Guid ChangedBy { get; set; }
        public string? ChangedByName { get; set; }
        public DateTime ChangedAt { get; set; }
    }
}
