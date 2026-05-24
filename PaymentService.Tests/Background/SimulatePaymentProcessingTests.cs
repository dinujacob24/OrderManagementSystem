using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentService.Background;

namespace PaymentService.Tests.Background;

/// <summary>
/// Exercises OutboxConsumer.SimulatePaymentProcessing via reflection. The method is private and
/// the input DTO is a private nested type, so we reach into them rather than refactor production
/// code. If the source surface is ever made internal + [InternalsVisibleTo], delete the reflection.
/// </summary>
public class SimulatePaymentProcessingTests
{
    private static readonly Type ConsumerType = typeof(OutboxConsumer);
    private static readonly Type DtoType =
        ConsumerType.GetNestedType("ProcessPaymentCommandDto", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ProcessPaymentCommandDto nested type not found.");
    private static readonly MethodInfo SimulateMethod =
        ConsumerType.GetMethod("SimulatePaymentProcessing", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("SimulatePaymentProcessing method not found.");

    private static (bool success, string? errorMessage) Invoke(decimal amount, string customerId)
    {
        var dto = Activator.CreateInstance(DtoType)!;
        DtoType.GetProperty("Amount")!.SetValue(dto, amount);
        DtoType.GetProperty("CustomerId")!.SetValue(dto, customerId);
        DtoType.GetProperty("OrderId")!.SetValue(dto, 1);
        DtoType.GetProperty("SagaId")!.SetValue(dto, Guid.NewGuid());

        var consumer = new OutboxConsumer(Mock.Of<IServiceProvider>(), NullLogger<OutboxConsumer>.Instance);

        var result = SimulateMethod.Invoke(consumer, new[] { dto })!;
        var rt = result.GetType();
        var success = (bool)rt.GetField("Item1")!.GetValue(result)!;
        var errorMessage = (string?)rt.GetField("Item2")!.GetValue(result);
        return (success, errorMessage);
    }

    [Fact]
    public void HappyPath_ReturnsSuccess()
    {
        var (success, error) = Invoke(99.50m, "cust-1");

        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(100.99)]
    [InlineData(0.99)]
    [InlineData(1.99)]
    public void AmountEndingIn99Cents_ReturnsInsufficientFunds(double amount)
    {
        var (success, error) = Invoke((decimal)amount, "cust-1");

        success.Should().BeFalse();
        error.Should().Be("Insufficient funds");
    }

    [Theory]
    [InlineData("user-fail-01")]
    [InlineData("USER-FAIL")]
    [InlineData("fail")]
    [InlineData("Failure")]
    public void CustomerIdContainsFail_CaseInsensitive_ReturnsCardDeclined(string customerId)
    {
        var (success, error) = Invoke(50m, customerId);

        success.Should().BeFalse();
        error.Should().Be("Payment card declined");
    }

    [Fact]
    public void AmountJustOverLimit_ReturnsLimitExceeded()
    {
        var (success, error) = Invoke(10000.01m, "cust-1");

        success.Should().BeFalse();
        error.Should().Be("Transaction amount exceeds limit");
    }

    [Fact]
    public void AmountExactlyAtLimit_Succeeds()
    {
        var (success, error) = Invoke(10000m, "cust-1");

        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void RulePrecedence_NinetyNineCents_BeatsFailCustomer()
    {
        var (success, error) = Invoke(50.99m, "fail-user");

        success.Should().BeFalse();
        error.Should().Be("Insufficient funds");
    }

    [Fact]
    public void RulePrecedence_FailCustomer_BeatsOverLimit()
    {
        var (success, error) = Invoke(20000m, "fail-user");

        success.Should().BeFalse();
        error.Should().Be("Payment card declined");
    }

    [Fact(Skip = "DEF-PAY-01: zero-amount payments are currently allowed. Left as documentation.")]
    public void ZeroAmount_ShouldBeRejected()
    {
        var (success, _) = Invoke(0m, "cust-1");
        success.Should().BeFalse();
    }

    [Fact(Skip = "DEF-PAY-01: negative-amount payments are currently allowed. Left as documentation.")]
    public void NegativeAmount_ShouldBeRejected()
    {
        var (success, _) = Invoke(-50m, "cust-1");
        success.Should().BeFalse();
    }
}
