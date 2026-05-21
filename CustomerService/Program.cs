using System.Text.Json;
using CustomerService.Common.Authentication;
using CustomerService.Common.Behaviors;
using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using CustomerService.Features.Authentication;
using CustomerService.Features.CreateCustomer;
using CustomerService.Features.DeactivateCustomer;
using CustomerService.Features.GetCustomerById;
using CustomerService.Features.ListCustomers;
using CustomerService.Features.ReactivateCustomer;
using CustomerService.Features.UpdateCustomer;
using CustomerService.Infrastructure.OrderClient;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shared.Logging;
using Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Central Logging — sends logs to LoggingService via HTTP
builder.AddCentralLogging("CustomerService");

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Customer Service API",
        Version = "v1",
        Description = "API for managing customers in the Order Management System"
    });

    // Add JWT Authentication to Swagger
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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
            new List<string>()
        }
    });
});

// Only register SQLite DbContext if not running tests (test environment uses InMemory database)
var isTesting = builder.Configuration.GetValue<bool>("Testing:UseInMemoryDatabase");
if (!isTesting)
{
    builder.Services.AddDbContext<CustomerDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.AddHealthChecks()
    .AddDbContextCheck<CustomerDbContext>(
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "db" });

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
typeAdapterConfig.Scan(typeof(Program).Assembly);
builder.Services.AddSingleton(typeAdapterConfig);
builder.Services.AddScoped<IMapper, ServiceMapper>();

// Add HttpContextAccessor for forwarding JWT tokens
builder.Services.AddHttpContextAccessor();

// Add Order Service HTTP client for cancelling orders when customer is deactivated
builder.Services.AddHttpClient<IOrderServiceClient, OrderServiceClient>(client =>
{
    var baseUrl = builder.Configuration["OrderService:BaseUrl"]
        ?? throw new InvalidOperationException("Missing configuration: OrderService:BaseUrl");
    client.BaseAddress = new Uri(baseUrl);
});

// Add JWT Token Generator
builder.Services.AddScoped<JwtTokenGenerator>();

// Add CORS to allow OrderService to call CustomerService
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

// Auto-apply migrations on startup so the database is always up to date.
// Skipped when tests use the InMemory provider — Migrate() requires a relational provider.
if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler(eh => eh.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;

    ProblemDetails problem = ex switch
    {
        FluentValidation.ValidationException ve => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Extensions =
            {
                ["errors"] = ve.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            }
        },
        NotFoundException nf => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Resource not found",
            Detail = nf.Message
        },
        ConflictException cf => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Resource conflict",
            Detail = cf.Message
        },
        DbUpdateException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Resource conflict",
            Detail = "A resource with the same identifier already exists."
        },
        BadHttpRequestException bre => new ProblemDetails
        {
            Status = bre.StatusCode == 0 ? StatusCodes.Status400BadRequest : bre.StatusCode,
            Title = "Invalid request",
            Detail = bre.Message
        },
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred."
        }
    };

    ctx.Response.StatusCode = problem.Status!.Value;
    ctx.Response.ContentType = "application/problem+json";
    await ctx.Response.WriteAsJsonAsync(problem);
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Customer Service API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// Attach/propagate X-Correlation-Id header on every request
app.UseMiddleware<CorrelationIdMiddleware>();

// Enable CORS before authentication
app.UseCors("AllowAll");

// Enable authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// --- Health checks ---
// /health           — full snapshot of every registered check
// /health/live      — liveness only (process is up; no dependencies probed)
// /health/ready     — readiness (only checks tagged "ready", e.g. database)
var healthWriter = async (HttpContext ctx, HealthReport report) =>
{
    ctx.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            durationMs = e.Value.Duration.TotalMilliseconds,
            description = e.Value.Description,
            error = e.Value.Exception?.Message,
            tags = e.Value.Tags
        })
    };
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload,
        new JsonSerializerOptions { WriteIndented = true }));
};

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = healthWriter
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = healthWriter
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = healthWriter
});

// --- Map vertical-slice minimal API endpoints ---
app.MapAuthentication();
app.MapCreateCustomer();
app.MapGetCustomerById();
app.MapListCustomers();
app.MapUpdateCustomer();
app.MapDeactivateCustomer();
app.MapReactivateCustomer();

app.Run();

public partial class Program { }

