using CustomerService.Common.Behaviors;
using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using CustomerService.Features.CreateCustomer;
using CustomerService.Features.DeactivateCustomer;
using CustomerService.Features.GetCustomerById;
using CustomerService.Features.ListCustomers;
using CustomerService.Features.UpdateCustomer;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
});

builder.Services.AddDbContext<CustomerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
typeAdapterConfig.Scan(typeof(Program).Assembly);
builder.Services.AddSingleton(typeAdapterConfig);
builder.Services.AddScoped<IMapper, ServiceMapper>();

var app = builder.Build();

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

// --- Map vertical-slice minimal API endpoints ---
app.MapCreateCustomer();
app.MapGetCustomerById();
app.MapListCustomers();
app.MapUpdateCustomer();
app.MapDeactivateCustomer();

app.Run();

