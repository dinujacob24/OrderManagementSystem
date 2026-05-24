using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Database;

namespace NotificationService.Tests.Helpers;

internal static class TestDbContextFactory
{
    public static NotificationDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new NotificationDbContext(options);
    }

    public static (NotificationDbContext Context, SqliteConnection Connection) CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new NotificationDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }
}
