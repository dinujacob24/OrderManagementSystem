using Microsoft.EntityFrameworkCore;
using PaymentService.Common.Authentication;
using PaymentService.Infrastructure.Database;
using Shared.Logging;
using Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Central Logging — sends logs to LoggingService via HTTP
builder.AddCentralLogging("PaymentService");

// Database Configuration - SQLite (shared database)
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Background Services - Outbox Pattern
builder.Services.AddHostedService<PaymentService.Background.OutboxConsumer>();

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "Payment Service API", Version = "v1" });

    // Add JWT Bearer Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(doc => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", doc, null)] = new List<string>()
    });
});


// Add JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Register health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Initialize shared database tables
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await SharedDatabaseInitializer.InitializeAsync(context);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Attach/propagate X-Correlation-Id header on every request
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

// Look for UseUrls, Kestrel endpoints, or launchSettings.json
var httpPort = builder.Configuration.GetValue<int>("HttpPort", 80);
var httpsPort = builder.Configuration.GetValue<int>("HttpsPort", 443);

app.Urls.Add($"http://localhost:{httpPort}");
app.Urls.Add($"https://localhost:{httpsPort}");

app.Run();
