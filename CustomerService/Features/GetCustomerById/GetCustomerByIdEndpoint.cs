using MediatR;

namespace CustomerService.Features.GetCustomerById;

public static class GetCustomerByIdEndpoint
{
    public static IEndpointRouteBuilder MapGetCustomerById(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers/{customerId}", async (
            string customerId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var response = await mediator.Send(new GetCustomerByIdQuery(customerId), ct);
            return Results.Ok(response);
        })
        .WithName("GetCustomerById")
        .WithTags("Customers")
        .Produces<GetCustomerByIdResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization();

        return app;
    }
}
