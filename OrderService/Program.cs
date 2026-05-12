using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Consumers;
using OrderService.Infrastructure.Database;
using OrderService.Saga;
using OrderService.Background;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration - SQLite
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// MediatR for CQRS
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly));

// Saga Orchestrator
builder.Services.AddScoped<OrderSagaOrchestrator>();

// Register outbox consumer as background service
builder.Services.AddHostedService<OutboxConsumer>();

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
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
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
