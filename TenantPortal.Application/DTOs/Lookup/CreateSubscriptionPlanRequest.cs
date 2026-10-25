using System.ComponentModel.DataAnnotations;
using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.DTOs.Lookup
{
    public sealed class CreateSubscriptionPlanRequest
    {
        [Required, StringLength(150, MinimumLength = 2)]
        public string Name { get; set; } = default!;

        // Travels as its enum NAME over JSON (global JsonStringEnumConverter
        // in Program.cs) — "Microsoft" or "Adrotech"; invalid values are
        // rejected by model binding itself, no separate validation needed.
        [Required]
        public SubscriptionPlanProvider Provider { get; set; }

        public int? Qty { get; set; }

        // Required — a plan with no price can't be billed, so this is no
        // longer optional. Minimum 0.01 so an omitted/defaulted 0 fails
        // validation instead of silently creating a free plan.
        [Range(0.01, 999999.99)]
        public decimal PricePerMonth { get; set; }
    }
}
