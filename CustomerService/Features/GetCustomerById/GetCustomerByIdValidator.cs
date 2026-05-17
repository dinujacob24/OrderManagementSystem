using FluentValidation;

namespace CustomerService.Features.GetCustomerById;

public class GetCustomerByIdValidator : AbstractValidator<GetCustomerByIdQuery>
{
    public GetCustomerByIdValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
    }
}
