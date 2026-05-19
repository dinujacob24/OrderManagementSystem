using MediatR;

namespace CustomerService.Features.CreateCustomer;

public static class CreateCustomerEndpoint
{
    public static IEndpointRouteBuilder MapCreateCustomer(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/customers", async (
            CreateCustomerRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var response = await mediator.Send(new CreateCustomerCommand(request), ct);
            return Results.Created($"/api/customers/{response.CustomerId}", response);
        })
        .WithName("CreateCustomer")
        .WithTags("Customers")
        .Produces<CreateCustomerResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .RequireAuthorization();

        return app;
    }
}