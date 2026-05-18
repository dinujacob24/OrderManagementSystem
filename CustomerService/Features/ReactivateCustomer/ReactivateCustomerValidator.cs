using FluentValidation;

namespace CustomerService.Features.ReactivateCustomer;

public class ReactivateCustomerValidator : AbstractValidator<ReactivateCustomerCommand>
{
    public ReactivateCustomerValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}
