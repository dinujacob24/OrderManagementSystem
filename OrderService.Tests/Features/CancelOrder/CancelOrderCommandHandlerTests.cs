using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.Domain;
using OrderService.Features.CancelOrder;
using OrderService.Tests.Helpers;

namespace OrderService.Tests.Features.CancelOrder;

public class CancelOrderCommandHandlerTests
{
    private static CancelOrderCommand Cmd(int orderId, string reason = "Customer requested") =>
        new() { OrderId = orderId, Reason = reason };

    [Fact]
    public async Task Handle_CancelsOrder_AndReturnsSuccess()
    {
        await using var db = TestDbContextFactory.Create();
        var existing = new OrderBuilder().Pending().Build();
        db.Orders.Add(existing);
        await db.SaveChangesAsync();
        var sut = new CancelOrderCommandHandler(db, NullLogger<CancelOrderCommandHandler>.Instance);

        var response = await sut.Handle(Cmd(existing.OrderId), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.OrderId.Should().Be(existing.OrderId);
        response.Message.Should().Contain("cancelled successfully");
    }

    [Fact]
    public async Task Handle_UpdatesOrderStatus_ToCancelled()
    {
        await using var db = TestDbContextFactory.Create();
        var existing = new OrderBuilder().Pending().Build();
        db.Orders.Add(existing);
        await db.SaveChangesAsync();
        var sut = new CancelOrderCommandHandler(db, NullLogger<CancelOrderCommandHandler>.Instance);

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
        var sut = new CancelOrderCommandHandler(db, NullLogger<CancelOrderCommandHandler>.Instance);
        var before = DateTime.UtcNow;

        await sut.Handle(Cmd(existing.OrderId), CancellationToken.None);

        var after = DateTime.UtcNow;
        var persisted = await db.Orders.SingleAsync();
        persisted.UpdatedAt.Should().NotBeNull();
        persisted.UpdatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_WritesOrderCancelledEvent_ToOutbox()
    {
        await using var db = TestDbContextFactory.Create();
        var existing = new OrderBuilder().WithCustomerId("CUST-007").Pending().Build();
        db.Orders.Add(existing);
        await db.SaveChangesAsync();
        var sut = new CancelOrderCommandHandler(db, NullLogger<CancelOrderCommandHandler>.Instance);

        await sut.Handle(Cmd(existing.OrderId, reason: "Out of stock"), CancellationToken.None);

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be("order-cancelled");
        outbox.Processed.Should().BeFalse();
        outbox.Payload.Should().Contain("CUST-007");
        outbox.Payload.Should().Contain("Out of stock");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenOrderNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new CancelOrderCommandHandler(db, NullLogger<CancelOrderCommandHandler>.Instance);

        var response = await sut.Handle(Cmd(orderId: 9999), CancellationToken.None);

        response.Success.Should().BeFalse();
        response.OrderId.Should().Be(9999);
        response.Message.Should().Be("Order not found");
    }

    [Fact]
    public async Task Handle_DoesNotWriteOutbox_WhenOrderNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new CancelOrderCommandHandler(db, NullLogger<CancelOrderCommandHandler>.Instance);

        await sut.Handle(Cmd(orderId: 9999), CancellationToken.None);

        (await db.OutboxMessages.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LeavesOtherOrdersUntouched()
    {
        await using var db = TestDbContextFactory.Create();
        var target = new OrderBuilder().WithCustomerId("CUST-001").Pending().Build();
        var other = new OrderBuilder().WithCustomerId("CUST-002").Pending().Build();
        db.Orders.AddRange(target, other);
        await db.SaveChangesAsync();
        var sut = new CancelOrderCommandHandler(db, NullLogger<CancelOrderCommandHandler>.Instance);

        await sut.Handle(Cmd(target.OrderId), CancellationToken.None);

        var otherReloaded = await db.Orders.SingleAsync(o => o.OrderId == other.OrderId);
        otherReloaded.Status.Should().Be(OrderStatus.Pending);
        otherReloaded.UpdatedAt.Should().BeNull();
    }
}
