using MediatR;

namespace CustomerService.Features.DeactivateCustomer;

public static class DeactivateCustomerEndpoint
{
    public static IEndpointRouteBuilder MapDeactivateCustomer(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/customers/{customerId}/deactivate", async (
            string customerId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var response = await mediator.Send(new DeactivateCustomerCommand(customerId), ct);
            return Results.Ok(response);
        })
        .WithName("DeactivateCustomer")
        .WithTags("Customers")
        .Produces<DeactivateCustomerResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();

        return app;
    }
}
