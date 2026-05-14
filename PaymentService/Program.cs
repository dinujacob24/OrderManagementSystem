using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Database Configuration - SQLite (shared database)
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Background Services - Outbox Pattern
builder.Services.AddHostedService<PaymentService.Background.OutboxConsumer>();

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

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
app.MapControllers();

app.Run();
