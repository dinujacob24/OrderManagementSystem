using CustomerService.Features.ReactivateCustomer;
using FluentValidation.TestHelper;

namespace CustomerService.Tests.Features.ReactivateCustomer;

public class ReactivateCustomerValidatorTests
{
    private readonly ReactivateCustomerValidator _sut = new();

    [Fact]
    public void Empty_id_fails() =>
        _sut.TestValidate(new ReactivateCustomerCommand(""))
            .ShouldHaveValidationErrorFor(x => x.CustomerId);

    [Fact]
    public void Non_empty_id_passes() =>
        _sut.TestValidate(new ReactivateCustomerCommand("CUST-001"))
            .ShouldNotHaveValidationErrorFor(x => x.CustomerId);
}
