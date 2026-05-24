using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrderDetailsService.Infrastructure.Database;

namespace OrderDetailsService.Tests.Helpers;

internal static class TestDbContextFactory
{
    public static OrderDetailsDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<OrderDetailsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new OrderDetailsDbContext(options);
    }

    public static (OrderDetailsDbContext Context, SqliteConnection Connection) CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<OrderDetailsDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new OrderDetailsDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }
}
