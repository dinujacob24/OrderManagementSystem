using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OrderDetailsService.Consumers;
using OrderDetailsService.Domain;
using OrderDetailsService.Tests.Helpers;
using Shared.Messages.Events;

namespace OrderDetailsService.Tests.Consumers;

public class OrderCancelledConsumerTests
{
    [Fact]
    public async Task ConsumeAsync_MarksAllMatchingItemsCancelled_AndSetsUpdatedAt()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.OrderItems.AddRange(
            new OrderItemBuilder().WithOrderId(5).WithProductId("A").WithStatus(OrderItemStatus.Added).Build(),
            new OrderItemBuilder().WithOrderId(5).WithProductId("B").WithStatus(OrderItemStatus.Added).Build());
        await db.SaveChangesAsync();
        var sut = new OrderCancelledConsumer(db, NullLogger<OrderCancelledConsumer>.Instance);

        await sut.ConsumeAsync(new OrderCancelledEvent { OrderId = 5, CustomerId = "c", Reason = "r" });

        var items = await db.OrderItems.OrderBy(i => i.ProductId).ToListAsync();
        items.Should().AllSatisfy(i =>
        {
            i.Status.Should().Be("Cancelled");
            i.UpdatedAt.Should().NotBeNull();
            i.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        });
    }

    [Fact]
    public async Task ConsumeAsync_IsNoOp_WhenNoItemsMatch()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.OrderItems.Add(new OrderItemBuilder().WithOrderId(99).WithStatus(OrderItemStatus.Added).Build());
        await db.SaveChangesAsync();
        var sut = new OrderCancelledConsumer(db, NullLogger<OrderCancelledConsumer>.Instance);

        await sut.ConsumeAsync(new OrderCancelledEvent { OrderId = 5, CustomerId = "c" });

        var item = await db.OrderItems.SingleAsync();
        item.Status.Should().Be(OrderItemStatus.Added);
        item.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task ConsumeAsync_DoesNotCancelItems_FromOtherOrders()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.OrderItems.AddRange(
            new OrderItemBuilder().WithOrderId(1).WithProductId("MINE").WithStatus(OrderItemStatus.Added).Build(),
            new OrderItemBuilder().WithOrderId(2).WithProductId("OTHER").WithStatus(OrderItemStatus.Added).Build());
        await db.SaveChangesAsync();
        var sut = new OrderCancelledConsumer(db, NullLogger<OrderCancelledConsumer>.Instance);

        await sut.ConsumeAsync(new OrderCancelledEvent { OrderId = 1, CustomerId = "c" });

        var mine = await db.OrderItems.SingleAsync(i => i.ProductId == "MINE");
        var other = await db.OrderItems.SingleAsync(i => i.ProductId == "OTHER");
        mine.Status.Should().Be("Cancelled");
        other.Status.Should().Be(OrderItemStatus.Added);
        other.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task ConsumeAsync_RefreshesUpdatedAt_EvenWhenItemAlreadyCancelled()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var alreadyCancelled = new OrderItemBuilder()
            .WithOrderId(7)
            .WithStatus(OrderItemStatus.Cancelled)
            .WithUpdatedAt(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .Build();
        db.OrderItems.Add(alreadyCancelled);
        await db.SaveChangesAsync();
        var sut = new OrderCancelledConsumer(db, NullLogger<OrderCancelledConsumer>.Instance);

        await sut.ConsumeAsync(new OrderCancelledEvent { OrderId = 7, CustomerId = "c" });

        var item = await db.OrderItems.SingleAsync();
        item.Status.Should().Be("Cancelled");
        item.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
