using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentService.Consumers;
using PaymentService.Tests.Helpers;
using Shared.Messages.Commands;
using Shared.Messages.Events;

namespace PaymentService.Tests.Consumers;

public class ProcessRefundConsumerTests
{
    [Fact]
    public async Task ConsumeAsync_WhenPaymentExists_InsertsRefundWithCompletedStatus()
    {
        await using var db = TestDbContextFactory.Create();
        var payment = new PaymentBuilder().WithPaymentId(101).WithOrderId(42).WithAmount(200m).Build();
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var sut = new ProcessRefundConsumer(db, NullLogger<ProcessRefundConsumer>.Instance);
        var command = new ProcessRefundCommand
        {
            OrderId = 42,
            PaymentId = 101,
            Amount = 200m,
            CorrelationId = Guid.NewGuid()
        };

        await sut.ConsumeAsync(command);

        var refund = await db.Refunds.SingleAsync();
        refund.Status.Should().Be("Completed");
        refund.OrderId.Should().Be(42);
        refund.PaymentId.Should().Be(payment.PaymentId);
        refund.Amount.Should().Be(200m);
        refund.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConsumeAsync_WhenPaymentExists_EmitsRefundCompletedOutboxMessage()
    {
        await using var db = TestDbContextFactory.Create();
        var payment = new PaymentBuilder().WithPaymentId(7).WithOrderId(15).WithAmount(75m).Build();
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        var correlationId = Guid.NewGuid();

        var sut = new ProcessRefundConsumer(db, NullLogger<ProcessRefundConsumer>.Instance);
        var command = new ProcessRefundCommand
        {
            OrderId = 15,
            PaymentId = 7,
            Amount = 75m,
            CorrelationId = correlationId
        };

        await sut.ConsumeAsync(command);

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be("refund-completed");
        outbox.Processed.Should().BeFalse();

        var evt = JsonSerializer.Deserialize<RefundCompletedEvent>(outbox.Payload)!;
        evt.Success.Should().BeTrue();
        evt.OrderId.Should().Be(15);
        evt.PaymentId.Should().Be(7);
        evt.RefundAmount.Should().Be(75m);
        evt.CorrelationId.Should().Be(correlationId);
        evt.Message.Should().Be("Refund processed successfully");
    }

    [Fact]
    public async Task ConsumeAsync_WhenPaymentNotFound_DoesNotInsertRefund()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new ProcessRefundConsumer(db, NullLogger<ProcessRefundConsumer>.Instance);
        var command = new ProcessRefundCommand
        {
            OrderId = 999,
            PaymentId = 1,
            Amount = 50m,
            CorrelationId = Guid.NewGuid()
        };

        await sut.ConsumeAsync(command);

        (await db.Refunds.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ConsumeAsync_WhenPaymentNotFound_EmitsFailureOutboxMessage()
    {
        await using var db = TestDbContextFactory.Create();
        var correlationId = Guid.NewGuid();
        var sut = new ProcessRefundConsumer(db, NullLogger<ProcessRefundConsumer>.Instance);
        var command = new ProcessRefundCommand
        {
            OrderId = 999,
            PaymentId = 88,
            Amount = 50m,
            CorrelationId = correlationId
        };

        await sut.ConsumeAsync(command);

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be("refund-completed");

        var evt = JsonSerializer.Deserialize<RefundCompletedEvent>(outbox.Payload)!;
        evt.Success.Should().BeFalse();
        evt.Message.Should().Be("Payment not found");
        evt.OrderId.Should().Be(999);
        evt.PaymentId.Should().Be(88);
        evt.RefundAmount.Should().Be(50m);
        evt.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task ConsumeAsync_WhenPaymentNotFound_LogsWarning()
    {
        await using var db = TestDbContextFactory.Create();
        var logger = new Mock<ILogger<ProcessRefundConsumer>>();

        var sut = new ProcessRefundConsumer(db, logger.Object);
        await sut.ConsumeAsync(new ProcessRefundCommand { OrderId = 12345, Amount = 10m });

        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Payment not found")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_PropagatesCorrelationIdIntoOutboxEvent()
    {
        await using var db = TestDbContextFactory.Create();
        var payment = new PaymentBuilder().WithPaymentId(11).WithOrderId(11).Build();
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        var correlationId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var sut = new ProcessRefundConsumer(db, NullLogger<ProcessRefundConsumer>.Instance);
        await sut.ConsumeAsync(new ProcessRefundCommand
        {
            OrderId = 11,
            PaymentId = 11,
            Amount = 10m,
            CorrelationId = correlationId
        });

        var outbox = await db.OutboxMessages.SingleAsync();
        var evt = JsonSerializer.Deserialize<RefundCompletedEvent>(outbox.Payload)!;
        evt.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task ConsumeAsync_RefundPaymentId_BindsToFoundPaymentNotCommand()
    {
        // Doc-defect: the consumer uses payment.PaymentId from the lookup, not command.PaymentId.
        await using var db = TestDbContextFactory.Create();
        var payment = new PaymentBuilder().WithPaymentId(7).WithOrderId(50).Build();
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var sut = new ProcessRefundConsumer(db, NullLogger<ProcessRefundConsumer>.Instance);
        await sut.ConsumeAsync(new ProcessRefundCommand
        {
            OrderId = 50,
            PaymentId = 99,
            Amount = 100m,
            CorrelationId = Guid.NewGuid()
        });

        var refund = await db.Refunds.SingleAsync();
        refund.PaymentId.Should().Be(7);
    }

    [Fact]
    public async Task ConsumeAsync_RefundAmount_UsesCommandAmountVerbatim()
    {
        await using var db = TestDbContextFactory.Create();
        var payment = new PaymentBuilder().WithPaymentId(1).WithOrderId(1).WithAmount(999m).Build();
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var sut = new ProcessRefundConsumer(db, NullLogger<ProcessRefundConsumer>.Instance);
        await sut.ConsumeAsync(new ProcessRefundCommand
        {
            OrderId = 1,
            PaymentId = 1,
            Amount = 250.55m,
            CorrelationId = Guid.NewGuid()
        });

        var refund = await db.Refunds.SingleAsync();
        refund.Amount.Should().Be(250.55m);
    }

    [Fact(Skip = "DEF-PAY-02: ProcessRefundConsumer is not idempotent — calling twice yields two refund rows. Test left to document the defect.")]
    public async Task ConsumeAsync_CalledTwice_ShouldNotCreateDuplicateRefund()
    {
        await using var db = TestDbContextFactory.Create();
        var payment = new PaymentBuilder().WithPaymentId(1).WithOrderId(1).Build();
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var sut = new ProcessRefundConsumer(db, NullLogger<ProcessRefundConsumer>.Instance);
        var command = new ProcessRefundCommand
        {
            OrderId = 1,
            PaymentId = 1,
            Amount = 10m,
            CorrelationId = Guid.NewGuid()
        };

        await sut.ConsumeAsync(command);
        await sut.ConsumeAsync(command);

        (await db.Refunds.CountAsync()).Should().Be(1);
    }
}
