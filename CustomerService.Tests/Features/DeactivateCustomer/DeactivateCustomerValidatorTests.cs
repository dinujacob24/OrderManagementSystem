using CustomerService.Features.DeactivateCustomer;
using FluentValidation.TestHelper;

namespace CustomerService.Tests.Features.DeactivateCustomer;

public class DeactivateCustomerValidatorTests
{
    private readonly DeactivateCustomerValidator _sut = new();

    [Fact]
    public void Empty_id_fails() =>
        _sut.TestValidate(new DeactivateCustomerCommand(""))
            .ShouldHaveValidationErrorFor(x => x.CustomerId);

    [Fact]
    public void Non_empty_id_passes() =>
        _sut.TestValidate(new DeactivateCustomerCommand("CUST-001"))
            .ShouldNotHaveValidationErrorFor(x => x.CustomerId);
}
