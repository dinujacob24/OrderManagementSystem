using Microsoft.EntityFrameworkCore;
using NotificationService.Background;
using NotificationService.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlite(connectionString));

// Register consumers for DI
builder.Services.AddScoped<NotificationService.Consumers.OrderCancelledConsumer>();

// Background services
builder.Services.AddHostedService<OutboxConsumer>();

var app = builder.Build();

// Initialize shared database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await SharedDatabaseInitializer.InitializeAsync(db);
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
