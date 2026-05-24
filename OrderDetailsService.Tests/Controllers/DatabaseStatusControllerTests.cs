using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderDetailsService.Controllers;
using OrderDetailsService.Tests.Helpers;

namespace OrderDetailsService.Tests.Controllers;

public class DatabaseStatusControllerTests
{
    [Fact]
    public async Task GetStatus_ReturnsConnected_WithCounts()
    {
        var (db, conn) = TestDbContextFactory.CreateSqlite();
        await using var _ = db;
        using var __ = conn;
        db.OrderItems.Add(new OrderItemBuilder().WithOrderId(1).Build());
        db.OrderItems.Add(new OrderItemBuilder().WithOrderId(2).Build());
        await db.SaveChangesAsync();
        var sut = new DatabaseStatusController(db);

        var result = await sut.GetStatus();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value!;
        body.GetType().GetProperty("status")!.GetValue(body).Should().Be("Connected");

        var tables = body.GetType().GetProperty("tables")!.GetValue(body)!;
        tables.GetType().GetProperty("orderItems")!.GetValue(tables).Should().Be(2);
        tables.GetType().GetProperty("outboxMessages")!.GetValue(tables).Should().Be(0);
    }

    [Fact]
    public async Task RecreateDatabase_DropsAndRecreates_ResultingInEmptyTables()
    {
        var (db, conn) = TestDbContextFactory.CreateSqlite();
        await using var _ = db;
        using var __ = conn;
        db.OrderItems.Add(new OrderItemBuilder().Build());
        await db.SaveChangesAsync();
        var sut = new DatabaseStatusController(db);

        var result = await sut.RecreateDatabase();

        result.Should().BeOfType<OkObjectResult>();
        (await db.OrderItems.CountAsync()).Should().Be(0);
    }
}
