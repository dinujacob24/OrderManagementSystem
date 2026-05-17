using CustomerService.Features.UpdateCustomer;
using FluentValidation.TestHelper;

namespace CustomerService.Tests.Features.UpdateCustomer;

public class UpdateCustomerValidatorTests
{
    private readonly UpdateCustomerValidator _sut = new();

    private static UpdateCustomerCommand Cmd(
        string id = "CUST-001",
        string firstName = "Alice",
        string lastName = "Smith",
        string email = "alice@example.com",
        string? phone = null) =>
        new(id, new UpdateCustomerRequest(firstName, lastName, email, phone, null, null, null));

    [Fact]
    public void Valid_command_passes() =>
        _sut.TestValidate(Cmd()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void CustomerId_empty_fails() =>
        _sut.TestValidate(Cmd(id: "")).ShouldHaveValidationErrorFor(x => x.CustomerId);

    [Fact]
    public void FirstName_empty_fails() =>
        _sut.TestValidate(Cmd(firstName: "")).ShouldHaveValidationErrorFor(x => x.Request.FirstName);

    [Fact]
    public void FirstName_too_long_fails() =>
        _sut.TestValidate(Cmd(firstName: new string('a', 101)))
            .ShouldHaveValidationErrorFor(x => x.Request.FirstName);

    [Fact]
    public void LastName_empty_fails() =>
        _sut.TestValidate(Cmd(lastName: "")).ShouldHaveValidationErrorFor(x => x.Request.LastName);

    [Fact]
    public void LastName_too_long_fails() =>
        _sut.TestValidate(Cmd(lastName: new string('a', 101)))
            .ShouldHaveValidationErrorFor(x => x.Request.LastName);

    [Fact]
    public void Email_empty_fails() =>
        _sut.TestValidate(Cmd(email: "")).ShouldHaveValidationErrorFor(x => x.Request.Email);

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at-sign.com")]
    public void Email_invalid_fails(string email) =>
        _sut.TestValidate(Cmd(email: email)).ShouldHaveValidationErrorFor(x => x.Request.Email);

    [Fact]
    public void Email_too_long_fails() =>
        _sut.TestValidate(Cmd(email: new string('a', 200) + "@x.com"))
            .ShouldHaveValidationErrorFor(x => x.Request.Email);

    [Fact]
    public void Phone_null_passes() =>
        _sut.TestValidate(Cmd(phone: null))
            .ShouldNotHaveValidationErrorFor(x => x.Request.Phone);

    [Fact]
    public void Phone_too_long_fails() =>
        _sut.TestValidate(Cmd(phone: new string('5', 21)))
            .ShouldHaveValidationErrorFor(x => x.Request.Phone);
}
