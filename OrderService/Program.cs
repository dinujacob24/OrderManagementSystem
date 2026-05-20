using MassTransit;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OrderService.Common.Authentication;
using OrderService.Consumers;
using OrderService.Infrastructure.CustomerClient;
using OrderService.Infrastructure.Database;
using OrderService.Saga;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration - SQLite
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// MediatR for CQRS
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly));

// Add HttpContextAccessor for forwarding JWT tokens
builder.Services.AddHttpContextAccessor();

// Customer Service HTTP client (Option A — synchronous customer validation)
builder.Services.AddHttpClient<ICustomerValidationClient, CustomerValidationClient>(client =>
{
    var baseUrl = builder.Configuration["CustomerService:BaseUrl"]
        ?? throw new InvalidOperationException("Missing configuration: CustomerService:BaseUrl");
    client.BaseAddress = new Uri(baseUrl);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();

    // In development, bypass SSL certificate validation for self-signed certificates
    if (builder.Environment.IsDevelopment())
    {
        handler.ServerCertificateCustomValidationCallback = 
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }

    return handler;
});

// Saga Orchestrator
builder.Services.AddScoped<OrderSagaOrchestrator>();

// Background Services - Outbox Pattern
builder.Services.AddHostedService<OrderService.Background.OutboxConsumer>();
builder.Services.AddHostedService<OrderService.Background.OutboxDispatcher>();

// MassTransit - Message Bus Configuration
builder.Services.AddMassTransit(x =>
{
    // Register Consumers
    x.AddConsumer<OrderDetailsCompletedConsumer>();
    x.AddConsumer<PaymentCompletedConsumer>();
    x.AddConsumer<NotificationCompletedConsumer>();

    // Use InMemory for development/testing
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });

    // For production, use RabbitMQ:
    // x.UsingRabbitMq((context, cfg) =>
    // {
    //     cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
    //     {
    //         h.Username(builder.Configuration["RabbitMQ:Username"]);
    //         h.Password(builder.Configuration["RabbitMQ:Password"]);
    //     });
    //     cfg.ConfigureEndpoints(context);
    // });

    // Or Azure Service Bus:
    // x.UsingAzureServiceBus((context, cfg) =>
    // {
    //     cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);
    //     cfg.ConfigureEndpoints(context);
    // });
});

// Register health checks
builder.Services.AddHealthChecks();
// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo { Title = "Order Service API", Version = "v1" });

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

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429; // Ensure 429 is returned on rejection
    options.AddPolicy("user", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.IsAuthenticated == true
                ? context.User.Identity.Name
                : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: key => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }
        )
    );
});

var app = builder.Build();

// Apply pending migrations and create database
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        // Initialize shared database schema (all tables for both services)
        OrderService.Infrastructure.Database.SharedDatabaseInitializer.EnsureSharedDatabaseSchema(connectionString!, logger);

        logger.LogInformation("OrderService database initialization complete");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error initializing database");
        throw;
    }
}

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Use rate limiting middleware
app.UseRateLimiter();

// Apply rate limiting to all controllers
app.MapControllers().RequireRateLimiting("user");

// Map health check endpoint
app.MapHealthChecks("/health");

// Look for UseUrls, Kestrel endpoints, or launchSettings.json
var host = app.Services.GetService<IHost>();

if (host != null)
{
    // If using Kestrel, ensure it's configured to listen on the expected URLs
    var urls = host.Services.GetService<IConfiguration>()["Kestrel:Endpoints:Http:Url"];
    if (string.IsNullOrEmpty(urls))
    {
        var logger = host.Services.GetService<ILogger<Program>>();
        logger.LogWarning("Kestrel URL configuration not found. Listen URLs: {Urls}", urls);
    }
    else
    {
        app.Urls.Add(urls);
    }
}

app.Run();
