using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Domain.Entities
{
    public class InvitationToken
    {
        [Key]
        public Guid TokenId { get; set; }
        public Guid UserId { get; set; }
        public byte[] TokenHash { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
    }
}