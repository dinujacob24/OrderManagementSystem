using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OrderDetailsService.Domain;
using OrderDetailsService.Features.GetOrderItems;
using OrderDetailsService.Tests.Helpers;

namespace OrderDetailsService.Tests.Features.GetOrderItems;

public class GetOrderItemsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsAllItems_ForRequestedOrderId()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.OrderItems.AddRange(
            new OrderItemBuilder().WithOrderId(1).WithProductId("P-A").Build(),
            new OrderItemBuilder().WithOrderId(1).WithProductId("P-B").Build());
        await db.SaveChangesAsync();
        var sut = new GetOrderItemsQueryHandler(db, NullLogger<GetOrderItemsQueryHandler>.Instance);

        var result = await sut.Handle(new GetOrderItemsQuery(1), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.ProductId).Should().BeEquivalentTo(new[] { "P-A", "P-B" });
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoItemsMatch()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.OrderItems.Add(new OrderItemBuilder().WithOrderId(99).Build());
        await db.SaveChangesAsync();
        var sut = new GetOrderItemsQueryHandler(db, NullLogger<GetOrderItemsQueryHandler>.Instance);

        var result = await sut.Handle(new GetOrderItemsQuery(1), CancellationToken.None);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DoesNotLeakItems_FromOtherOrders()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.OrderItems.AddRange(
            new OrderItemBuilder().WithOrderId(1).WithProductId("MINE").Build(),
            new OrderItemBuilder().WithOrderId(2).WithProductId("OTHER").Build());
        await db.SaveChangesAsync();
        var sut = new GetOrderItemsQueryHandler(db, NullLogger<GetOrderItemsQueryHandler>.Instance);

        var result = await sut.Handle(new GetOrderItemsQuery(1), CancellationToken.None);

        result.Should().ContainSingle().Which.ProductId.Should().Be("MINE");
    }

    [Fact]
    public async Task Handle_MapsAllFields_ToResponse()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var createdAt = new DateTime(2026, 3, 14, 10, 30, 0, DateTimeKind.Utc);
        db.OrderItems.Add(new OrderItemBuilder()
            .WithOrderId(7)
            .WithProductId("SKU-7")
            .WithProductName("Widget")
            .WithQuantity(3)
            .WithUnitPrice(12.50m)
            .WithTotalPrice(37.50m)
            .WithStatus(OrderItemStatus.Added)
            .WithCreatedAt(createdAt)
            .Build());
        await db.SaveChangesAsync();
        var sut = new GetOrderItemsQueryHandler(db, NullLogger<GetOrderItemsQueryHandler>.Instance);

        var result = await sut.Handle(new GetOrderItemsQuery(7), CancellationToken.None);

        var item = result.Should().ContainSingle().Subject;
        item.OrderId.Should().Be(7);
        item.ProductId.Should().Be("SKU-7");
        item.ProductName.Should().Be("Widget");
        item.Quantity.Should().Be(3);
        item.UnitPrice.Should().Be(12.50m);
        item.TotalPrice.Should().Be(37.50m);
        item.Status.Should().Be(OrderItemStatus.Added);
        item.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task Handle_ThrowsOperationCanceled_WhenTokenAlreadyCancelled()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.OrderItems.Add(new OrderItemBuilder().WithOrderId(1).Build());
        await db.SaveChangesAsync();
        var sut = new GetOrderItemsQueryHandler(db, NullLogger<GetOrderItemsQueryHandler>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await sut.Handle(new GetOrderItemsQuery(1), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
