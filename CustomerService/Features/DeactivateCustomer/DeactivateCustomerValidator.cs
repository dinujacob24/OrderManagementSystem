using FluentValidation;

namespace CustomerService.Features.DeactivateCustomer;

public class DeactivateCustomerValidator : AbstractValidator<DeactivateCustomerCommand>
{
    public DeactivateCustomerValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}
