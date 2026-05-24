using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Background;
using NotificationService.Domain;
using NotificationService.Infrastructure.Database;
using Shared.Messages;
using Shared.Messages.Events;

namespace NotificationService.Tests.Background;

public class OutboxConsumerTests
{
    [Fact]
    public async Task SendNotification_CreatesNotification_AndQueuesCompletedEvent()
    {
        await using var db = CreateInMemoryDb();
        var sut = CreateConsumer(BuildServiceProviderForDb(db));

        var command = new
        {
            SagaId = Guid.NewGuid(),
            OrderId = 101,
            CustomerId = "cust-101",
            Message = "Your order is on its way",
            NotificationType = "Email",
            Timestamp = DateTime.UtcNow
        };

        await InvokeSendNotification(sut, db, command, CancellationToken.None);

        var notification = await db.Notifications.SingleAsync();
        notification.SagaId.Should().Be(command.SagaId);
        notification.OrderId.Should().Be(command.OrderId);
        notification.CustomerId.Should().Be(command.CustomerId);
        notification.Message.Should().Be(command.Message);
        notification.NotificationType.Should().Be("Email");
        notification.Status.Should().Be("Sent");
        notification.Recipient.Should().Be("cust-101@example.com");
        notification.ErrorMessage.Should().BeNull();
        notification.CreatedAt.Should().NotBe(default);
        notification.SentAt.Should().NotBeNull();

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be(MessageTypes.NotificationCompletedEvent);
        outbox.Processed.Should().BeFalse();
        var evt = JsonSerializer.Deserialize<NotificationCompletedEvent>(outbox.Payload)!;
        evt.SagaId.Should().Be(command.SagaId);
        evt.OrderId.Should().Be(command.OrderId);
        evt.Success.Should().BeTrue();
        evt.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ProcessOrderCancelledEvent_CreatesSentNotification()
    {
        await using var db = CreateInMemoryDb();
        var sut = CreateConsumer(BuildServiceProviderForDb(db));

        var evt = new OrderCancelledEvent
        {
            OrderId = 202,
            CustomerId = "cust-202",
            Reason = "Out of stock",
            CancelledAt = DateTime.UtcNow,
            SagaId = Guid.NewGuid()
        };

        await InvokeProcessOrderCancelledEvent(sut, db, evt, CancellationToken.None);

        var notification = await db.Notifications.SingleAsync();
        notification.OrderId.Should().Be(202);
        notification.CustomerId.Should().Be("cust-202");
        notification.NotificationType.Should().Be("Email");
        notification.Status.Should().Be("Sent");
        notification.Recipient.Should().Be("cust-202@example.com");
        notification.Message.Should().Contain("Out of stock");
        notification.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesPendingSendNotificationCommand_FromOutbox()
    {
        await using var fixture = SqliteFixture.Create();
        var saga = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.SendNotificationCommand,
                Payload = JsonSerializer.Serialize(new
                {
                    SagaId = saga,
                    OrderId = 11,
                    CustomerId = "cust-11",
                    Message = "Order received",
                    NotificationType = "Email",
                    Timestamp = DateTime.UtcNow
                }),
                Processed = false,
                CreatedAt = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        var sut = new OutboxConsumer(fixture.Services, NullLogger<OutboxConsumer>.Instance);
        SetInterval(sut, TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var check = fixture.CreateDbContext();
                return await check.OutboxMessages
                    .AnyAsync(o => o.MessageType == MessageTypes.SendNotificationCommand && o.Processed);
            });
        }
        finally
        {
            cts.Cancel();
            await sut.StopAsync(CancellationToken.None);
        }

        await using var db = fixture.CreateDbContext();
        var notification = await db.Notifications.SingleAsync(n => n.SagaId == saga);
        notification.OrderId.Should().Be(11);
        notification.Status.Should().Be("Sent");

        var commandRow = await db.OutboxMessages
            .SingleAsync(o => o.MessageType == MessageTypes.SendNotificationCommand);
        commandRow.Processed.Should().BeTrue();
        commandRow.ProcessedAt.Should().NotBeNull();
        commandRow.LockToken.Should().BeNull();
        commandRow.LastError.Should().BeNull();

        var completedEvent = await db.OutboxMessages
            .SingleAsync(o => o.MessageType == MessageTypes.NotificationCompletedEvent);
        completedEvent.Processed.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesOrderCancelledMessage_FromOutbox()
    {
        await using var fixture = SqliteFixture.Create();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = "order-cancelled",
                Payload = JsonSerializer.Serialize(new OrderCancelledEvent
                {
                    OrderId = 55,
                    CustomerId = "cust-55",
                    Reason = "Address invalid",
                    CancelledAt = DateTime.UtcNow,
                    SagaId = Guid.NewGuid()
                }),
                Processed = false,
                CreatedAt = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        var sut = new OutboxConsumer(fixture.Services, NullLogger<OutboxConsumer>.Instance);
        SetInterval(sut, TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var check = fixture.CreateDbContext();
                return await check.Notifications.AnyAsync(n => n.OrderId == 55);
            });
        }
        finally
        {
            cts.Cancel();
            await sut.StopAsync(CancellationToken.None);
        }

        await using var db = fixture.CreateDbContext();
        var notification = await db.Notifications.SingleAsync(n => n.OrderId == 55);
        notification.Status.Should().Be("Sent");
        notification.Message.Should().Contain("Address invalid");

        var outboxRow = await db.OutboxMessages.SingleAsync(o => o.MessageType == "order-cancelled");
        outboxRow.Processed.Should().BeTrue();
        outboxRow.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotPickUpOtherMessageTypes()
    {
        await using var fixture = SqliteFixture.Create();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = MessageTypes.OrderCreatedEvent,
                Payload = "{}",
                Processed = false,
                CreatedAt = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        var sut = new OutboxConsumer(fixture.Services, NullLogger<OutboxConsumer>.Instance);
        SetInterval(sut, TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        try
        {
            await Task.Delay(400);
        }
        finally
        {
            cts.Cancel();
            await sut.StopAsync(CancellationToken.None);
        }

        await using var db = fixture.CreateDbContext();
        (await db.Notifications.AnyAsync()).Should().BeFalse();
        var unrelated = await db.OutboxMessages.SingleAsync();
        unrelated.Processed.Should().BeFalse();
        unrelated.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_MarksMessageProcessed_AfterMaxAttempts_OnRepeatedFailure()
    {
        await using var fixture = SqliteFixture.Create();
        var msgId = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDbContext())
        {
            seedDb.OutboxMessages.Add(new OutboxMessage
            {
                Id = msgId,
                MessageType = MessageTypes.SendNotificationCommand,
                Payload = "{not valid json",
                Processed = false,
                CreatedAt = DateTime.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        var sut = new OutboxConsumer(fixture.Services, NullLogger<OutboxConsumer>.Instance);
        SetInterval(sut, TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        try
        {
            await WaitUntilAsync(async () =>
            {
                await using var check = fixture.CreateDbContext();
                var row = await check.OutboxMessages.FindAsync(msgId);
                return row is not null && row.Processed;
            }, timeoutSeconds: 15);
        }
        finally
        {
            cts.Cancel();
            await sut.StopAsync(CancellationToken.None);
        }

        await using var db = fixture.CreateDbContext();
        var row = await db.OutboxMessages.FindAsync(msgId);
        row.Should().NotBeNull();
        row!.Processed.Should().BeTrue();
        row.Attempts.Should().BeGreaterOrEqualTo(5);
        row.LastError.Should().NotBeNullOrEmpty();
        row.ProcessedAt.Should().NotBeNull();
    }

    private static NotificationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new NotificationDbContext(options);
    }

    private static IServiceProvider BuildServiceProviderForDb(NotificationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static OutboxConsumer CreateConsumer(IServiceProvider services)
        => new(services, NullLogger<OutboxConsumer>.Instance);

    private static void SetInterval(OutboxConsumer consumer, TimeSpan interval)
    {
        var field = typeof(OutboxConsumer).GetField("_interval", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(consumer, interval);
    }

    private static async Task InvokeSendNotification(OutboxConsumer consumer, NotificationDbContext db, object commandShape, CancellationToken ct)
    {
        var dtoType = typeof(OutboxConsumer)
            .GetNestedType("SendNotificationCommandDto", BindingFlags.NonPublic)!;
        var dto = Activator.CreateInstance(dtoType)!;
        foreach (var prop in commandShape.GetType().GetProperties())
        {
            dtoType.GetProperty(prop.Name)!.SetValue(dto, prop.GetValue(commandShape));
        }

        var method = typeof(OutboxConsumer).GetMethod(
            "SendNotification",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(consumer, new object[] { db, dto, ct })!;
    }

    private static async Task InvokeProcessOrderCancelledEvent(OutboxConsumer consumer, NotificationDbContext db, OrderCancelledEvent evt, CancellationToken ct)
    {
        var method = typeof(OutboxConsumer).GetMethod(
            "ProcessOrderCancelledEvent",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(consumer, new object[] { db, evt, ct })!;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await predicate()) return;
            await Task.Delay(50);
        }

        throw new TimeoutException($"Condition not met within {timeoutSeconds} seconds.");
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;
        private readonly string _connectionString;
        public IServiceProvider Services { get; }

        private SqliteFixture(SqliteConnection keepAlive, string connectionString, IServiceProvider services)
        {
            _keepAlive = keepAlive;
            _connectionString = connectionString;
            Services = services;
        }

        public static SqliteFixture Create()
        {
            var dbName = $"notif_test_{Guid.NewGuid():N}";
            var connectionString = $"DataSource=file:{dbName}?mode=memory&cache=shared";

            var keepAlive = new SqliteConnection(connectionString);
            keepAlive.Open();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<NotificationDbContext>(opt => opt.UseSqlite(connectionString));
            var provider = services.BuildServiceProvider();

            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                db.Database.EnsureCreated();
            }

            return new SqliteFixture(keepAlive, connectionString, provider);
        }

        public NotificationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<NotificationDbContext>()
                .UseSqlite(_connectionString)
                .Options;
            return new NotificationDbContext(options);
        }

        public ValueTask DisposeAsync()
        {
            _keepAlive.Close();
            _keepAlive.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
