using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{

    public class Session
    {
        [Key]
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public Guid? TenantId { get; set; }   // must be Guid? to match DB uniqueidentifier
        public byte[] RefreshTokenHash { get; set; } = default!;
        public Guid Jti { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? UserAgent { get; set; }
        public string? IP { get; set; }
    }

}