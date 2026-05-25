
        namespace TenantPortal.Application.DTOs
        {
            public record UserWithSubscriptionDto(Guid UserId, string Name, string Email, Guid TenantId, string SubscriptionName);
        }

