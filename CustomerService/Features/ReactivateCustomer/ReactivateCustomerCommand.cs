using MediatR;

namespace CustomerService.Features.ReactivateCustomer;

public record ReactivateCustomerCommand(string CustomerId) : IRequest<ReactivateCustomerResponse>;
