using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Consumers;
using OrderService.Infrastructure.Database;
using OrderService.Saga;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration - SQLite
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// MediatR for CQRS
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly));

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
    c.SwaggerDoc("v1", new() { Title = "Order Service API", Version = "v1" });
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

app.UseAuthorization();

app.MapControllers();

app.Run();
