using FluentAssertions;
using PaymentService.Domain;

namespace PaymentService.Tests.Domain;

public class PaymentTests
{
    [Fact]
    public void Payment_DefaultStatus_IsPending()
    {
        var sut = new Payment();

        sut.Status.Should().Be("Pending");
    }

    [Fact]
    public void Payment_DefaultCustomerId_IsEmptyString()
    {
        var sut = new Payment();

        sut.CustomerId.Should().BeEmpty();
    }

    [Fact]
    public void Payment_OptionalFields_DefaultToNull()
    {
        var sut = new Payment();

        sut.PaymentMethod.Should().BeNull();
        sut.TransactionId.Should().BeNull();
        sut.ErrorMessage.Should().BeNull();
        sut.ProcessedAt.Should().BeNull();
    }
}

public class RefundTests
{
    [Fact]
    public void Refund_DefaultStatus_IsEmptyString()
    {
        var sut = new Refund();

        sut.Status.Should().BeEmpty();
    }
}

public class OutboxMessageTests
{
    [Fact]
    public void OutboxMessage_DefaultProcessed_IsFalse()
    {
        var sut = new OutboxMessage();

        sut.Processed.Should().BeFalse();
        sut.Attempts.Should().Be(0);
    }

    [Fact]
    public void OutboxMessage_DefaultMessageTypeAndPayload_AreEmpty()
    {
        var sut = new OutboxMessage();

        sut.MessageType.Should().BeEmpty();
        sut.Payload.Should().BeEmpty();
    }
}
