using CustomerService.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Tests.Helpers;

internal static class TestDbContextFactory
{
    public static CustomerDbContext Create()
    {
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CustomerDbContext(options);
    }
}
