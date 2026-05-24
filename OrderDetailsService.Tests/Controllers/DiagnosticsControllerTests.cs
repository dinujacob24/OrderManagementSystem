using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using OrderDetailsService.Controllers;
using OrderDetailsService.Infrastructure.Database;
using OrderDetailsService.Tests.Helpers;

namespace OrderDetailsService.Tests.Controllers;

public class DiagnosticsControllerTests
{
    [Fact]
    public async Task GetOutboxMessages_ReturnsLatest20_OrderedByCreatedAtDescending()
    {
        var (db, conn) = TestDbContextFactory.CreateSqlite();
        await using var _ = db;
        using var __ = conn;
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 25; i++)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = "TypeA",
                Payload = $"{{\"i\":{i}}}",
                CreatedAt = baseTime.AddMinutes(i),
                Processed = false
            });
        }
        await db.SaveChangesAsync();
        var sut = new DiagnosticsController(db);

        var result = await sut.GetOutboxMessages() as OkObjectResult;

        result.Should().NotBeNull();
        var body = result!.Value!;
        var totalMessages = (int)body.GetType().GetProperty("totalMessages")!.GetValue(body)!;
        totalMessages.Should().Be(20);
    }

    [Fact]
    public async Task GetOutboxMessages_CountsOnly_UnprocessedOrderCreatedEvents()
    {
        var (db, conn) = TestDbContextFactory.CreateSqlite();
        await using var _ = db;
        using var __ = conn;
        db.OutboxMessages.AddRange(
            new OutboxMessage { Id = Guid.NewGuid(), MessageType = "OrderCreatedEvent", Payload = "{}", Processed = false, CreatedAt = DateTime.UtcNow },
            new OutboxMessage { Id = Guid.NewGuid(), MessageType = "OrderCreatedEvent", Payload = "{}", Processed = false, CreatedAt = DateTime.UtcNow },
            new OutboxMessage { Id = Guid.NewGuid(), MessageType = "OrderCreatedEvent", Payload = "{}", Processed = true,  CreatedAt = DateTime.UtcNow },
            new OutboxMessage { Id = Guid.NewGuid(), MessageType = "OrderDetailsCompletedEvent", Payload = "{}", Processed = false, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var sut = new DiagnosticsController(db);

        var result = await sut.GetOutboxMessages() as OkObjectResult;

        var body = result!.Value!;
        var unprocessed = (int)body.GetType().GetProperty("unprocessedOrderCreatedEvents")!.GetValue(body)!;
        unprocessed.Should().Be(2);
    }

    [Fact]
    public async Task GetOutboxMessages_ReturnsZero_WhenOutboxEmpty()
    {
        var (db, conn) = TestDbContextFactory.CreateSqlite();
        await using var _ = db;
        using var __ = conn;
        var sut = new DiagnosticsController(db);

        var result = await sut.GetOutboxMessages() as OkObjectResult;

        var body = result!.Value!;
        ((int)body.GetType().GetProperty("totalMessages")!.GetValue(body)!).Should().Be(0);
        ((int)body.GetType().GetProperty("unprocessedOrderCreatedEvents")!.GetValue(body)!).Should().Be(0);
    }
}
