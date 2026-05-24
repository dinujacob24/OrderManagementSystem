using System.Text.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderDetailsService.Consumers;
using OrderDetailsService.Domain;
using OrderDetailsService.Tests.Helpers;
using Shared.Messages.Events;

namespace OrderDetailsService.Tests.Consumers;

public class OrderCreatedConsumerTests
{
    [Fact]
    public async Task Consume_HappyPath_AddsItems_AndQueuesCompletedEvent()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var sut = new OrderCreatedConsumer(db, publishEndpoint.Object, NullLogger<OrderCreatedConsumer>.Instance);

        var sagaId = Guid.NewGuid();
        var message = new OrderCreatedEvent
        {
            SagaId = sagaId,
            OrderId = 10,
            CustomerId = "cust-10",
            TotalAmount = 60m,
            Items = new List<OrderItemEventDto>
            {
                new() { ProductId = "P-1", ProductName = "Item 1", Quantity = 2, UnitPrice = 10m },
                new() { ProductId = "P-2", ProductName = "Item 2", Quantity = 4, UnitPrice = 10m }
            }
        };

        await sut.Consume(BuildContext(message));

        var items = await db.OrderItems.OrderBy(i => i.ProductId).ToListAsync();
        items.Should().HaveCount(2);
        items[0].OrderId.Should().Be(10);
        items[0].ProductId.Should().Be("P-1");
        items[0].Quantity.Should().Be(2);
        items[0].UnitPrice.Should().Be(10m);
        items[0].TotalPrice.Should().Be(20m);
        items[0].Status.Should().Be(OrderItemStatus.Added);
        items[0].CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        items[1].TotalPrice.Should().Be(40m);

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be(nameof(OrderDetailsCompletedEvent));
        outbox.Processed.Should().BeFalse();
        var payload = JsonSerializer.Deserialize<OrderDetailsCompletedEvent>(outbox.Payload)!;
        payload.SagaId.Should().Be(sagaId);
        payload.OrderId.Should().Be(10);
        payload.Success.Should().BeTrue();
        payload.ErrorMessage.Should().BeNull();
        payload.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Consume_WritesFailureToOutbox_WhenItemsListIsNull()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var sut = new OrderCreatedConsumer(db, publishEndpoint.Object, NullLogger<OrderCreatedConsumer>.Instance);

        var message = new OrderCreatedEvent
        {
            SagaId = Guid.NewGuid(),
            OrderId = 11,
            CustomerId = "cust-11",
            Items = null!
        };

        await sut.Consume(BuildContext(message));

        (await db.OrderItems.AnyAsync()).Should().BeFalse();
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be(nameof(OrderDetailsFailedEvent));
        var payload = JsonSerializer.Deserialize<OrderDetailsFailedEvent>(outbox.Payload)!;
        payload.OrderId.Should().Be(11);
        payload.Reason.Should().Be("No items to add");
    }

    [Fact]
    public async Task Consume_WritesFailureToOutbox_WhenItemsListIsEmpty()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var sut = new OrderCreatedConsumer(db, publishEndpoint.Object, NullLogger<OrderCreatedConsumer>.Instance);

        var message = new OrderCreatedEvent
        {
            SagaId = Guid.NewGuid(),
            OrderId = 12,
            CustomerId = "cust-12",
            Items = new List<OrderItemEventDto>()
        };

        await sut.Consume(BuildContext(message));

        (await db.OrderItems.AnyAsync()).Should().BeFalse();
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be(nameof(OrderDetailsFailedEvent));
    }

    [Fact]
    public async Task Consume_WhenItemsAlreadyExist_PublishesSuccess_AndAddsNoNewItems()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        db.OrderItems.Add(new OrderItemBuilder().WithOrderId(20).WithProductId("EXISTING").Build());
        await db.SaveChangesAsync();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var sut = new OrderCreatedConsumer(db, publishEndpoint.Object, NullLogger<OrderCreatedConsumer>.Instance);

        var message = new OrderCreatedEvent
        {
            SagaId = Guid.NewGuid(),
            OrderId = 20,
            CustomerId = "cust-20",
            Items = new List<OrderItemEventDto>
            {
                new() { ProductId = "NEW", ProductName = "Should not be added", Quantity = 1, UnitPrice = 5m }
            }
        };

        await sut.Consume(BuildContext(message));

        var items = await db.OrderItems.ToListAsync();
        items.Should().ContainSingle().Which.ProductId.Should().Be("EXISTING");
        (await db.OutboxMessages.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Consume_PreservesSagaId_InOutboxPayload()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var sut = new OrderCreatedConsumer(db, publishEndpoint.Object, NullLogger<OrderCreatedConsumer>.Instance);

        var sagaId = Guid.NewGuid();
        var message = new OrderCreatedEvent
        {
            SagaId = sagaId,
            OrderId = 30,
            CustomerId = "cust-30",
            Items = new List<OrderItemEventDto>
            {
                new() { ProductId = "P-X", ProductName = "X", Quantity = 1, UnitPrice = 1m }
            }
        };

        await sut.Consume(BuildContext(message));

        var outbox = await db.OutboxMessages.SingleAsync();
        var payload = JsonSerializer.Deserialize<OrderDetailsCompletedEvent>(outbox.Payload)!;
        payload.SagaId.Should().Be(sagaId);
    }

    private static ConsumeContext<OrderCreatedEvent> BuildContext(OrderCreatedEvent message)
    {
        var ctx = new Mock<ConsumeContext<OrderCreatedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        return ctx.Object;
    }
}
