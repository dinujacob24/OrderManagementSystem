using CustomerService.Features.GetCustomerById;
using FluentValidation.TestHelper;

namespace CustomerService.Tests.Features.GetCustomerById;

public class GetCustomerByIdValidatorTests
{
    private readonly GetCustomerByIdValidator _sut = new();

    [Fact]
    public void Empty_id_fails() =>
        _sut.TestValidate(new GetCustomerByIdQuery(""))
            .ShouldHaveValidationErrorFor(x => x.CustomerId);

    [Fact]
    public void Non_empty_id_passes() =>
        _sut.TestValidate(new GetCustomerByIdQuery("CUST-001"))
            .ShouldNotHaveValidationErrorFor(x => x.CustomerId);
}
