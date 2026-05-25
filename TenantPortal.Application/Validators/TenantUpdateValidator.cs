//using FluentValidation;

//public sealed class TenantUpdateValidator : AbstractValidator<TenantUpdateRequest>
//{
//    public TenantUpdateValidator()
//    {
//        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(150);
//        RuleFor(x => x.EmailDomain).NotEmpty().MaximumLength(100);
//        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
//        RuleFor(x => x.LanguageId).GreaterThan(0);
//        RuleFor(x => x.BackupPlan).NotEmpty().MaximumLength(20);
//        RuleFor(x => x.Timezone).NotEmpty().MaximumLength(100);
//        RuleFor(x => x.City).GreaterThan(0);
//        RuleFor(x => x.Address).NotEmpty().MaximumLength(255);
//        RuleFor(x => x.PrimaryContact).NotEmpty().MaximumLength(150);
//        RuleFor(x => x.ContactNumber).NotEmpty().MaximumLength(20);
//    }
//}