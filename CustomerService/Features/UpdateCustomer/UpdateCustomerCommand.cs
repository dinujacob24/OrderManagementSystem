using MediatR;

namespace CustomerService.Features.UpdateCustomer;

public record UpdateCustomerCommand(string CustomerId, UpdateCustomerRequest Request)
    : IRequest<UpdateCustomerResponse>;
