using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Controllers;
using NotificationService.Domain;
using NotificationService.Tests.Helpers;
using Shared.Messages;

namespace NotificationService.Tests.Controllers;

public class DiagnosticsControllerTests
{
    [Fact]
    public async Task GetStatus_ReturnsCounts_AggregatedFromNotificationsAndOutbox()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        db.Notifications.AddRange(
            new Notification { OrderId = 1, Status = "Sent", CreatedAt = DateTime.UtcNow },
            new Notification { OrderId = 2, Status = "Sent", CreatedAt = DateTime.UtcNow },
            new Notification { OrderId = 3, Status = "Failed", CreatedAt = DateTime.UtcNow },
            new Notification { OrderId = 4, Status = "Pending", CreatedAt = DateTime.UtcNow }
        );

        db.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.SendNotificationCommand,
                Processed = false,
                CreatedAt = DateTime.UtcNow
            },
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.SendNotificationCommand,
                Processed = true,
                CreatedAt = DateTime.UtcNow
            },
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = nameof(Shared.Messages.Events.NotificationCompletedEvent),
                Processed = false,
                CreatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetStatus();

        var payload = AssertOk(result);
        Prop<int>(payload, "totalNotifications").Should().Be(4);
        Prop<int>(payload, "sentNotifications").Should().Be(2);
        Prop<int>(payload, "failedNotifications").Should().Be(1);
        Prop<int>(payload, "pendingCommands").Should().Be(1);
        Prop<int>(payload, "pendingEvents").Should().Be(1);
    }

    [Fact]
    public async Task GetStatus_WithEmptyDb_ReturnsZeroes()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetStatus();

        var payload = AssertOk(result);
        Prop<int>(payload, "totalNotifications").Should().Be(0);
        Prop<int>(payload, "sentNotifications").Should().Be(0);
        Prop<int>(payload, "failedNotifications").Should().Be(0);
        Prop<int>(payload, "pendingCommands").Should().Be(0);
        Prop<int>(payload, "pendingEvents").Should().Be(0);
    }

    [Fact]
    public async Task GetNotifications_WithoutFilter_ReturnsAllOrderedNewestFirst()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        db.Notifications.AddRange(
            new Notification { OrderId = 1, Status = "Sent", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Notification { OrderId = 2, Status = "Sent", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new Notification { OrderId = 3, Status = "Sent", CreatedAt = DateTime.UtcNow.AddMinutes(-5) }
        );
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetNotifications();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var notifications = ok.Value.Should().BeAssignableTo<IEnumerable<Notification>>().Subject.ToList();
        notifications.Should().HaveCount(3);
        notifications[0].OrderId.Should().Be(2);
        notifications[1].OrderId.Should().Be(3);
        notifications[2].OrderId.Should().Be(1);
    }

    [Fact]
    public async Task GetNotifications_WithOrderIdFilter_ReturnsOnlyMatching()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        db.Notifications.AddRange(
            new Notification { OrderId = 1, Status = "Sent", CreatedAt = DateTime.UtcNow },
            new Notification { OrderId = 2, Status = "Sent", CreatedAt = DateTime.UtcNow },
            new Notification { OrderId = 1, Status = "Failed", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetNotifications(orderId: 1);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var notifications = ok.Value.Should().BeAssignableTo<IEnumerable<Notification>>().Subject.ToList();
        notifications.Should().HaveCount(2);
        notifications.Should().OnlyContain(n => n.OrderId == 1);
    }

    [Fact]
    public async Task GetNotifications_CapsResultAt50()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        for (var i = 0; i < 75; i++)
        {
            db.Notifications.Add(new Notification
            {
                OrderId = i,
                Status = "Sent",
                CreatedAt = DateTime.UtcNow.AddSeconds(-i)
            });
        }
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetNotifications();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var notifications = ok.Value.Should().BeAssignableTo<IEnumerable<Notification>>().Subject.ToList();
        notifications.Should().HaveCount(50);
    }

    [Fact]
    public async Task GetOutbox_WithoutFilter_ReturnsAllOrderedNewestFirst()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        db.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.SendNotificationCommand,
                Processed = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.NotificationCompletedEvent,
                Processed = true,
                CreatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetOutbox();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = ((System.Collections.IEnumerable)ok.Value!).Cast<object>().ToList();
        messages.Should().HaveCount(2);
        Prop<string>(messages[0], "MessageType").Should().Be(MessageTypes.NotificationCompletedEvent);
        Prop<string>(messages[1], "MessageType").Should().Be(MessageTypes.SendNotificationCommand);
    }

    [Fact]
    public async Task GetOutbox_WithMessageTypeFilter_ReturnsOnlyMatching()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        db.OutboxMessages.AddRange(
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.SendNotificationCommand,
                Processed = false,
                CreatedAt = DateTime.UtcNow
            },
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.NotificationCompletedEvent,
                Processed = true,
                CreatedAt = DateTime.UtcNow
            },
            new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.SendNotificationCommand,
                Processed = true,
                CreatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetOutbox(messageType: MessageTypes.SendNotificationCommand);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = ((System.Collections.IEnumerable)ok.Value!).Cast<object>().ToList();
        messages.Should().HaveCount(2);
        foreach (var m in messages)
        {
            Prop<string>(m, "MessageType").Should().Be(MessageTypes.SendNotificationCommand);
        }
    }

    [Fact]
    public async Task GetOutbox_WithEmptyMessageType_TreatsAsNoFilter()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = MessageTypes.SendNotificationCommand,
            Processed = false,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetOutbox(messageType: string.Empty);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = ((System.Collections.IEnumerable)ok.Value!).Cast<object>().ToList();
        messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOutbox_CapsResultAt50()
    {
        await using var db = TestDbContextFactory.CreateInMemory();

        for (var i = 0; i < 75; i++)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.SendNotificationCommand,
                Processed = false,
                CreatedAt = DateTime.UtcNow.AddSeconds(-i)
            });
        }
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetOutbox();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = ((System.Collections.IEnumerable)ok.Value!).Cast<object>().ToList();
        messages.Should().HaveCount(50);
    }

    [Fact]
    public async Task GetNotification_ById_ReturnsExpectedNotification()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var notification = new Notification
        {
            OrderId = 99,
            CustomerId = "cust",
            Status = "Sent",
            Message = "hello",
            CreatedAt = DateTime.UtcNow
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetNotification(notification.NotificationId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = ok.Value.Should().BeOfType<Notification>().Subject;
        returned.NotificationId.Should().Be(notification.NotificationId);
        returned.OrderId.Should().Be(99);
    }

    [Fact]
    public async Task GetNotification_UnknownId_ReturnsNotFound()
    {
        await using var db = TestDbContextFactory.CreateInMemory();
        var sut = new DiagnosticsController(db, NullLogger<DiagnosticsController>.Instance);

        var result = await sut.GetNotification(9999);

        result.Should().BeOfType<NotFoundResult>();
    }

    private static object AssertOk(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value!;
    }

    private static T Prop<T>(object source, string name)
    {
        var prop = source.GetType().GetProperty(name)
            ?? throw new InvalidOperationException($"Property '{name}' not found on {source.GetType().FullName}");
        return (T)prop.GetValue(source)!;
    }
}
