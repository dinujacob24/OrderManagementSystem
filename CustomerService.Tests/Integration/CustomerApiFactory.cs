using CustomerService.Common.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CustomerService.Tests.Integration;

public class CustomerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.Single(
                d => d.ServiceType == typeof(DbContextOptions<CustomerDbContext>));
            services.Remove(dbContextDescriptor);

            services.AddDbContext<CustomerDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }

    public CustomerDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
    }
}
