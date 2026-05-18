using MediatR;

namespace CustomerService.Features.ReactivateCustomer;

public static class ReactivateCustomerEndpoint
{
    public static IEndpointRouteBuilder MapReactivateCustomer(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/customers/{customerId}/reactivate", async (
            string customerId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var response = await mediator.Send(new ReactivateCustomerCommand(customerId), ct);
            return Results.Ok(response);
        })
        .WithName("ReactivateCustomer")
        .WithTags("Customers")
        .Produces<ReactivateCustomerResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
