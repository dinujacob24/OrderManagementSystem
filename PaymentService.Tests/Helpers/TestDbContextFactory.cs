using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure.Database;

namespace PaymentService.Tests.Helpers;

internal static class TestDbContextFactory
{
    public static PaymentDbContext Create()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase($"payment-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        return new PaymentDbContext(options);
    }
}
