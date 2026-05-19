using MediatR;

namespace CustomerService.Features.UpdateCustomer;

public static class UpdateCustomerEndpoint
{
    public static IEndpointRouteBuilder MapUpdateCustomer(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/customers/{customerId}", async (
            string customerId,
            UpdateCustomerRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var response = await mediator.Send(new UpdateCustomerCommand(customerId, request), ct);
            return Results.Ok(response);
        })
        .WithName("UpdateCustomer")
        .WithTags("Customers")
        .Produces<UpdateCustomerResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem()
        .RequireAuthorization();

        return app;
    }
}
