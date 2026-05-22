using CustomerService.Common.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Claims;

namespace CustomerService.Tests.Integration;

public class CustomerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    static CustomerApiFactory()
    {
        // Program.cs reads this synchronously during builder construction, which happens
        // BEFORE WebApplicationFactory's ConfigureAppConfiguration callbacks run. Setting it
        // as an env var (with the ASP.NET Core "__" → ":" mapping) guarantees Program.cs
        // sees the flag and skips the SQLite DbContext registration.
        Environment.SetEnvironmentVariable("Testing__UseInMemoryDatabase", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<CustomerDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });


            // Replace JWT Bearer authentication with test authentication that always succeeds
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Create a test principal for authentication
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.Name, "TestUser"),
                            new Claim(ClaimTypes.NameIdentifier, "test-user-id")
                        };
                        var identity = new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme);
                        var principal = new ClaimsPrincipal(identity);

                        context.Principal = principal;
                        context.Success();
                        return Task.CompletedTask;
                    }
                };
            });
        });
    }

    public CustomerDbContext CreateDbContext()
    {
        // Create a fresh DbContext with only InMemory provider to avoid provider conflicts
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        return new CustomerDbContext(options);
    }
}
