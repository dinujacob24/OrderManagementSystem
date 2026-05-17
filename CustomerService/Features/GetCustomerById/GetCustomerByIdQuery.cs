using MediatR;

namespace CustomerService.Features.GetCustomerById;

public record GetCustomerByIdQuery(string CustomerId)
    : IRequest<GetCustomerByIdResponse>;
