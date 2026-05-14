using FluentValidation;

namespace CustomerService.Features.CreateCustomer;

public class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Request.CustomerId)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^CUST-[0-9]+$").WithMessage("CustomerId must match 'CUST-###' format.");

        RuleFor(x => x.Request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Request.Phone).MaximumLength(20).When(x => x.Request.Phone is not null);
    }
}
