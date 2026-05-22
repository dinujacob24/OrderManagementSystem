using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OrderService.Background;
using OrderService.Infrastructure.CustomerClient;
using OrderService.Infrastructure.Database;

namespace OrderService.Tests.Integration;

public class OrderApiFactory : WebApplicationFactory<Program>
{
    private readonly string _sqliteTempPath = Path.Combine(Path.GetTempPath(), $"ordersvc-test-{Guid.NewGuid():N}.db");
    public const string JwtSecret = "TestSecretKeyForIntegrationTesting1234567890ABCDEF";
    public const string JwtIssuer = "OrderManagementSystem";
    public const string JwtAudience = "OrderManagementSystem";

    public StubCustomerValidationClient CustomerStub { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_sqliteTempPath}",
                ["CustomerService:BaseUrl"] = "http://customer-test/",
                ["JwtSettings:SecretKey"] = JwtSecret,
                ["JwtSettings:Issuer"] = JwtIssuer,
                ["JwtSettings:Audience"] = JwtAudience
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove outbox-pollers so they don't race with assertions
            foreach (var d in services.Where(d =>
                         d.ServiceType == typeof(IHostedService) &&
                         (d.ImplementationType == typeof(OutboxConsumer) || d.ImplementationType == typeof(OutboxDispatcher))).ToList())
            {
                services.Remove(d);
            }

            // Stub the customer-validation HTTP client
            foreach (var d in services.Where(d => d.ServiceType == typeof(ICustomerValidationClient)).ToList())
            {
                services.Remove(d);
            }
            services.AddSingleton<ICustomerValidationClient>(CustomerStub);

            // JwtBearerOptions are configured during Program.cs with the appsettings.json
            // secret, before our config overrides apply. Repoint the validation key here.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
                options.TokenValidationParameters.ValidIssuer = JwtIssuer;
                options.TokenValidationParameters.ValidAudience = JwtAudience;
            });
        });
    }

    public OrderDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    }

    public string GenerateJwtToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: new[] { new Claim(ClaimTypes.Name, "test-user") },
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateJwtToken());
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (File.Exists(_sqliteTempPath)) File.Delete(_sqliteTempPath); }
        catch { /* best-effort */ }
    }
}

public class StubCustomerValidationClient : ICustomerValidationClient
{
    public bool ShouldSucceed { get; set; } = true;
    public string? FailureReason { get; set; }

    public Task<CustomerValidationResult> ValidateAsync(string customerId, CancellationToken ct) =>
        Task.FromResult(ShouldSucceed
            ? CustomerValidationResult.Ok()
            : CustomerValidationResult.Fail(FailureReason ?? $"Customer '{customerId}' not active."));
}
