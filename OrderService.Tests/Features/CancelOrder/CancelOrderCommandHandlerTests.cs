using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Domain;
using OrderService.Features.CancelOrder;
using OrderService.Saga;
using OrderService.Tests.Helpers;
using Shared.Messages.Events;

namespace OrderService.Tests.Features.CancelOrder;

public class CancelOrderCommandHandlerTests
{
    private static CancelOrderCommand Cmd(int orderId, string reason = "Customer requested") =>
        new() { OrderId = orderId, Reason = reason };

    private static (CancelOrderCommandHandler sut, Mock<IPublishEndpoint> publish)
        CreateSut(Infrastructure.Database.OrderDbContext db)
    {
        var publish = new Mock<IPublishEndpoint>();
        var saga = new OrderSagaOrchestrator(db, publish.Object, NullLogger<OrderSagaOrchestrator>.Instance);
        var sut = new CancelOrderCommandHandler(db, saga, NullLogger<CancelOrderCommandHandler>.Instance);
        return (sut, publish);
    }

    [Fact]
    public async Task Handle_CancelsOrder_AndReturnsSuccess()
    {
        await using var db = TestDbContextFactory.Create();
        var existing = new OrderBuilder().Pending().Build();
        db.Orders.Add(existing);
        await db.SaveChangesAsync();
        var (sut, _) = CreateSut(db);

        var response = await sut.Handle(Cmd(existing.OrderId), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Message.Should().Contain("cancelled successfully");
    }

    [Fact]
    public async Task Handle_UpdatesOrderStatus_ToCancelled()
    {
        await using var db = TestDbContextFactory.Create();
        var existing = new OrderBuilder().Pending().Build();
        db.Orders.Add(existing);
        await db.SaveChangesAsync();
        var (sut, _) = CreateSut(db);

        await sut.Handle(Cmd(existing.OrderId), CancellationToken.None);

        var persisted = await db.Orders.SingleAsync();
        persisted.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_SetsUpdatedAt_ToRecentUtcNow()
    {
        await using var db = TestDbContextFactory.Create();
        var existing = new OrderBuilder().Pending().Build();
        db.Orders.Add(existing);
        await db.SaveChangesAsync();
        var (sut, _) = CreateSut(db);
        var before = DateTime.UtcNow;

        await sut.Handle(Cmd(existing.OrderId), CancellationToken.None);

        var after = DateTime.UtcNow;
        var persisted = await db.Orders.SingleAsync();
        persisted.UpdatedAt.Should().NotBeNull();
        persisted.UpdatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_InvokesSagaCompensation_WhenSagaStateExists()
    {
        await using var db = TestDbContextFactory.Create();
        var existing = new OrderBuilder().WithCustomerId("CUST-007").Pending().Build();
        db.Orders.Add(existing);
        await db.SaveChangesAsync();
        db.SagaStates.Add(new SagaState
        {
            SagaId = Guid.NewGuid(),
            OrderId = existing.OrderId,
            CustomerId = existing.CustomerId,
            CurrentStep = "OrderCreated",
            Status = OrderStatus.Pending,
            StartedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var (sut, publish) = CreateSut(db);

        await sut.Handle(Cmd(existing.OrderId, reason: "Out of stock"), CancellationToken.None);

        publish.Verify(
            p => p.Publish(
                It.Is<OrderCancelledEvent>(e =>
                    e.OrderId == existing.OrderId &&
                    e.CustomerId == "CUST-007"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenOrderNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var (sut, _) = CreateSut(db);

        var response = await sut.Handle(Cmd(orderId: 9999), CancellationToken.None);

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Order not found.");
    }

    [Fact]
    public async Task Handle_DoesNotInvokeSaga_WhenOrderNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var (sut, publish) = CreateSut(db);

        await sut.Handle(Cmd(orderId: 9999), CancellationToken.None);

        publish.Verify(
            p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_LeavesOtherOrdersUntouched()
    {
        await using var db = TestDbContextFactory.Create();
        var target = new OrderBuilder().WithCustomerId("CUST-001").Pending().Build();
        var other = new OrderBuilder().WithCustomerId("CUST-002").Pending().Build();
        db.Orders.AddRange(target, other);
        await db.SaveChangesAsync();
        var (sut, _) = CreateSut(db);

        await sut.Handle(Cmd(target.OrderId), CancellationToken.None);

        var otherReloaded = await db.Orders.SingleAsync(o => o.OrderId == other.OrderId);
        otherReloaded.Status.Should().Be(OrderStatus.Pending);
        otherReloaded.UpdatedAt.Should().BeNull();
    }
}
