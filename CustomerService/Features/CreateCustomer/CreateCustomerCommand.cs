using MediatR;

namespace CustomerService.Features.CreateCustomer;

public record CreateCustomerCommand(CreateCustomerRequest Request)
    : IRequest<CreateCustomerResponse>;
