using CustomerService.Features.CreateCustomer;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace CustomerService.Tests.Features.CreateCustomer;

public class CreateCustomerValidatorTests
{
    private readonly CreateCustomerValidator _sut = new();

    private static CreateCustomerCommand Cmd(
        string id = "CUST-001",
        string firstName = "Alice",
        string lastName = "Smith",
        string email = "alice@example.com",
        string? phone = null) =>
        new(new CreateCustomerRequest(id, firstName, lastName, email, phone, null, null, null));

    [Fact]
    public void Valid_command_passes()
    {
        var result = _sut.TestValidate(Cmd());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CustomerId_empty_fails(string id) =>
        _sut.TestValidate(Cmd(id: id)).ShouldHaveValidationErrorFor(x => x.Request.CustomerId);

    [Fact]
    public void CustomerId_too_long_fails() =>
        _sut.TestValidate(Cmd(id: "CUST-" + new string('1', 60)))
            .ShouldHaveValidationErrorFor(x => x.Request.CustomerId);

    [Theory]
    [InlineData("cust-001")]
    [InlineData("CUST_001")]
    [InlineData("CUST-")]
    [InlineData("CUST-ABC")]
    [InlineData("ORDER-001")]
    public void CustomerId_wrong_pattern_fails(string id) =>
        _sut.TestValidate(Cmd(id: id)).ShouldHaveValidationErrorFor(x => x.Request.CustomerId);

    [Theory]
    [InlineData("CUST-1")]
    [InlineData("CUST-001")]
    [InlineData("CUST-999999")]
    public void CustomerId_valid_pattern_passes(string id) =>
        _sut.TestValidate(Cmd(id: id)).ShouldNotHaveValidationErrorFor(x => x.Request.CustomerId);

    [Fact]
    public void FirstName_empty_fails() =>
        _sut.TestValidate(Cmd(firstName: "")).ShouldHaveValidationErrorFor(x => x.Request.FirstName);

    [Fact]
    public void FirstName_too_long_fails() =>
        _sut.TestValidate(Cmd(firstName: new string('a', 101)))
            .ShouldHaveValidationErrorFor(x => x.Request.FirstName);

    [Fact]
    public void FirstName_at_max_length_passes() =>
        _sut.TestValidate(Cmd(firstName: new string('a', 100)))
            .ShouldNotHaveValidationErrorFor(x => x.Request.FirstName);

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
    [InlineData("@no-local.com")]
    public void Email_invalid_format_fails(string email) =>
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

    [Fact]
    public void Phone_valid_passes() =>
        _sut.TestValidate(Cmd(phone: "+1-555-123-4567"))
            .ShouldNotHaveValidationErrorFor(x => x.Request.Phone);

    [Fact]
    public void Multiple_failures_all_surface()
    {
        var result = _sut.TestValidate(Cmd(id: "", firstName: "", email: "not-an-email"));
        result.ShouldHaveValidationErrorFor(x => x.Request.CustomerId);
        result.ShouldHaveValidationErrorFor(x => x.Request.FirstName);
        result.ShouldHaveValidationErrorFor(x => x.Request.Email);
    }
}
