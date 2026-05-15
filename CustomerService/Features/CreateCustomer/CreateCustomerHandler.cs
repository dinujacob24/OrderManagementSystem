using CustomerService.Common.Entities;
using CustomerService.Common.Enums;
using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Features.CreateCustomer;

public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, CreateCustomerResponse>
{
    private readonly CustomerDbContext _db;
    private readonly IMapper _mapper;

    public CreateCustomerHandler(CustomerDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<CreateCustomerResponse> Handle(
        CreateCustomerCommand cmd, CancellationToken ct)
    {
        if (await _db.Customers.AnyAsync(c => c.CustomerId == cmd.Request.CustomerId, ct))
        {
            throw new ConflictException($"Customer with id '{cmd.Request.CustomerId}' already exists.");
        }

        var customer = _mapper.Map<Customer>(cmd.Request);
        customer.Status = CustomerStatus.Active;
        customer.CreatedAt = DateTime.UtcNow;

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        return _mapper.Map<CreateCustomerResponse>(customer);
    }
}
