using MediatR;

namespace CustomerService.Features.ListCustomers;

public static class ListCustomersEndpoint
{
    public static IEndpointRouteBuilder MapListCustomers(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers", async (
            IMediator mediator,
            CancellationToken ct) =>
        {
            var response = await mediator.Send(new ListCustomersQuery(), ct);
            return Results.Ok(response);
        })
        .WithName("ListCustomers")
        .WithTags("Customers")
        .Produces<List<ListCustomersResponse>>()
        .RequireAuthorization();

        return app;
    }
}
