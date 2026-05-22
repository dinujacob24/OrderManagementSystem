using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Database;

namespace OrderService.Tests.Helpers;

internal static class TestDbContextFactory
{
    public static OrderDbContext Create()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new OrderDbContext(options);
    }
}
