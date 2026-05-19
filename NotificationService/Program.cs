using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NotificationService.Background;
using NotificationService.Common.Authentication;
using NotificationService.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Notification Service API", Version = "v1" });
});

// Add JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

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

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
