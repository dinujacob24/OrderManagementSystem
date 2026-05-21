using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderDetailsService.Common.Authentication;
using OrderDetailsService.Consumers;
using OrderDetailsService.Infrastructure.Database;
using Shared.Logging;
using Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Central Logging — sends logs to LoggingService via HTTP
builder.AddCentralLogging("OrderDetailsService");

// Database Configuration - SQLite
builder.Services.AddDbContext<OrderDetailsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// MediatR for CQRS
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly));

// Register consumers for DI
builder.Services.AddScoped<OrderCreatedConsumer>();
builder.Services.AddScoped<OrderCancelledConsumer>();

// Register outbox consumer as background service (consumes from OrderService)
builder.Services.AddHostedService<OrderDetailsService.Background.OutboxConsumer>();

// NOTE: OutboxDispatcher is NOT needed in shared database pattern
// OrderService will read OrderDetailsCompletedEvent directly from the shared OutboxMessages table
// builder.Services.AddHostedService<OrderDetailsService.Background.OutboxDispatcher>();

// MassTransit - Message Bus Configuration (NOT USED in shared database pattern)
// OrderCreatedConsumer is NOT registered because OrderService publishes to database outbox, not MassTransit
builder.Services.AddMassTransit(x =>
{
    // Use InMemory for development/testing
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
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
//});


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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Order Details Service API", Version = "v1" });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

// Apply pending migrations and create database
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderDetailsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        // Initialize shared database schema (all tables for both services)
        OrderDetailsService.Infrastructure.Database.SharedDatabaseInitializer.EnsureSharedDatabaseSchema(connectionString!, logger);

        logger.LogInformation("OrderDetailsService database initialization complete");
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

// Attach/propagate X-Correlation-Id header on every request
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
