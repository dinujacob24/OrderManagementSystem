using System;
using MediatR;

namespace CustomerService.Features.DeactivateCustomer;

public record DeactivateCustomerCommand(string CustomerId) : IRequest<DeactivateCustomerResponse>;
