using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Consumers;
using NotificationService.Tests.Helpers;
using Shared.Messages.Events;

namespace NotificationService.Tests.Consumers;

public class OrderCancelledConsumerTests
{
    [Fact]
    public async Task ConsumeAsync_PersistsNotification_WithExpectedFields()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var sut = new OrderCancelledConsumer(db, NullLogger<OrderCancelledConsumer>.Instance);

        var evt = new OrderCancelledEvent
        {
            OrderId = 42,
            CustomerId = "cust-1",
            Reason = "Customer requested",
            CancelledAt = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid(),
            SagaId = Guid.NewGuid()
        };

        await sut.ConsumeAsync(evt);

        var notification = await db.Notifications.SingleAsync();
        notification.OrderId.Should().Be(42);
        notification.CustomerId.Should().Be("cust-1");
        notification.NotificationType.Should().Be("Email");
        notification.Status.Should().Be("Sent");
        notification.Message.Should().Contain("42").And.Contain("Customer requested");
        notification.CreatedAt.Should().NotBe(default);
        notification.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ConsumeAsync_HandlesMultipleEvents_CreatingSeparateNotifications()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var sut = new OrderCancelledConsumer(db, NullLogger<OrderCancelledConsumer>.Instance);

        await sut.ConsumeAsync(new OrderCancelledEvent { OrderId = 1, CustomerId = "a", Reason = "r1" });
        await sut.ConsumeAsync(new OrderCancelledEvent { OrderId = 2, CustomerId = "b", Reason = "r2" });

        var notifications = await db.Notifications.OrderBy(n => n.OrderId).ToListAsync();
        notifications.Should().HaveCount(2);
        notifications[0].OrderId.Should().Be(1);
        notifications[1].OrderId.Should().Be(2);
    }

    [Fact]
    public async Task ConsumeAsync_AcceptsEmptyReason_AndStillCreatesNotification()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var sut = new OrderCancelledConsumer(db, NullLogger<OrderCancelledConsumer>.Instance);

        await sut.ConsumeAsync(new OrderCancelledEvent
        {
            OrderId = 7,
            CustomerId = "cust-7",
            Reason = string.Empty
        });

        var notification = await db.Notifications.SingleAsync();
        notification.Message.Should().Contain("Reason:");
    }
}
