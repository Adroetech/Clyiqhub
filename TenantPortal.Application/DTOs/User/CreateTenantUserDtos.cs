//namespace TenantPortal.Application.DTOs.User
//{
//    public sealed class CreateTenantUserRequest
//    {
//        public string Name { get; set; } = default!;
//        public string Email { get; set; } = default!;
//        public string Role { get; set; } = "environment_manager";  // Tenant Admin only
//        public Guid? TenantId { get; set; }                        // required for platform users
//    }

//    public sealed class CreateTenantUserResponse
//    {
//        public Guid UserId { get; set; }
//        public string Status { get; set; } = default!;
//    }

//    public sealed class RejectUserRequest
//    {
//        public string RejectionReason { get; set; } = default!;
//    }
//}