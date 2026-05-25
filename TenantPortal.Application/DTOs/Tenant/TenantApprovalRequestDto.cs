using System;
using System.Collections.Generic;
using System.Text;

namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantApprovalRequestDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Type { get; set; } // create, update
        public string Status { get; set; } // pending, approved, rejected
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
