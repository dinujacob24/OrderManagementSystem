using MediatR;

namespace CustomerService.Features.ListCustomers;

public record ListCustomersQuery() : IRequest<List<ListCustomersResponse>>;
