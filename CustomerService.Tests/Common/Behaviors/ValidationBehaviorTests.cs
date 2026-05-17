using CustomerService.Common.Behaviors;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;

namespace CustomerService.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    public record FakeRequest(string Value) : IRequest<string>;

    private class AlwaysPassingValidator : AbstractValidator<FakeRequest>
    {
        public AlwaysPassingValidator() => RuleFor(x => x.Value).NotEmpty();
    }

    private class CustomFailingValidator : AbstractValidator<FakeRequest>
    {
        public CustomFailingValidator()
        {
            RuleFor(x => x.Value).Must(_ => false).WithMessage("forced-failure");
        }
    }

    [Fact]
    public async Task NoValidators_CallsNext()
    {
        var sut = new ValidationBehavior<FakeRequest, string>(Array.Empty<IValidator<FakeRequest>>());
        var called = false;
        RequestHandlerDelegate<string> next = () => { called = true; return Task.FromResult("ok"); };

        var result = await sut.Handle(new FakeRequest("x"), next, CancellationToken.None);

        called.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task PassingValidators_CallsNext()
    {
        var sut = new ValidationBehavior<FakeRequest, string>(new[] { new AlwaysPassingValidator() });
        var called = false;
        RequestHandlerDelegate<string> next = () => { called = true; return Task.FromResult("ok"); };

        var result = await sut.Handle(new FakeRequest("non-empty"), next, CancellationToken.None);

        called.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task FailingValidator_ThrowsValidationException_AndDoesNotCallNext()
    {
        var sut = new ValidationBehavior<FakeRequest, string>(new[] { new CustomFailingValidator() });
        var called = false;
        RequestHandlerDelegate<string> next = () => { called = true; return Task.FromResult("ok"); };

        var act = () => sut.Handle(new FakeRequest("anything"), next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        called.Should().BeFalse();
    }

    [Fact]
    public async Task MultipleFailingValidators_AggregateErrors()
    {
        var v1 = new Mock<IValidator<FakeRequest>>();
        v1.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<FakeRequest>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Value", "err-1") }));
        var v2 = new Mock<IValidator<FakeRequest>>();
        v2.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<FakeRequest>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("Value", "err-2") }));
        var sut = new ValidationBehavior<FakeRequest, string>(new[] { v1.Object, v2.Object });

        var act = () => sut.Handle(new FakeRequest("anything"),
            () => Task.FromResult("ok"), CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Should().HaveCount(2);
        ex.Errors.Select(e => e.ErrorMessage).Should().Contain(new[] { "err-1", "err-2" });
    }

    [Fact]
    public async Task CancellationToken_PassedThrough()
    {
        var validator = new Mock<IValidator<FakeRequest>>();
        var token = new CancellationToken(canceled: false);
        validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<FakeRequest>>(), token))
                 .ReturnsAsync(new ValidationResult())
                 .Verifiable();
        var sut = new ValidationBehavior<FakeRequest, string>(new[] { validator.Object });

        await sut.Handle(new FakeRequest("x"), () => Task.FromResult("ok"), token);

        validator.Verify();
    }
}
