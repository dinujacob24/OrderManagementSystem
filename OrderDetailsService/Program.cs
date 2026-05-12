using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderDetailsService.Consumers;
using OrderDetailsService.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration - SQLite
builder.Services.AddDbContext<OrderDetailsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// MediatR for CQRS
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(Program).Assembly));

// Register consumers for DI
builder.Services.AddScoped<OrderCreatedConsumer>();

// Register outbox consumer as background service (consumes from OrderService)
builder.Services.AddHostedService<OrderDetailsService.Background.OutboxConsumer>();

// Register outbox dispatcher as background service (publishes OrderDetailsService events)
builder.Services.AddHostedService<OrderDetailsService.Background.OutboxDispatcher>();

// MassTransit - Message Bus Configuration
builder.Services.AddMassTransit(x =>
{
    // Register Consumers
    x.AddConsumer<OrderCreatedConsumer>();

    // Use InMemory for development/testing
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });

// Register outbox dispatcher as background service
builder.Services.AddHostedService<OrderDetailsService.Background.OutboxDispatcher>();

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
    c.SwaggerDoc("v1", new() { Title = "Order Details Service API", Version = "v1" });
});

var app = builder.Build();

// Apply pending migrations and create database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDetailsDbContext>();
    db.Database.EnsureCreated();
}

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.MapControllers();

app.Run();
