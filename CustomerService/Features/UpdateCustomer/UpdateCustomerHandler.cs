using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using MapsterMapper;
using MediatR;

namespace CustomerService.Features.UpdateCustomer;

public class UpdateCustomerHandler : IRequestHandler<UpdateCustomerCommand, UpdateCustomerResponse>
{
    private readonly CustomerDbContext _db;
    private readonly IMapper _mapper;

    public UpdateCustomerHandler(CustomerDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<UpdateCustomerResponse> Handle(
        UpdateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { cmd.CustomerId }, ct)
            ?? throw new NotFoundException("Customer", cmd.CustomerId);

        customer.FirstName = cmd.Request.FirstName;
        customer.LastName = cmd.Request.LastName;
        customer.Email = cmd.Request.Email;
        customer.Phone = cmd.Request.Phone;
        customer.AddressLine1 = cmd.Request.AddressLine1;
        customer.City = cmd.Request.City;
        customer.Country = cmd.Request.Country;
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return _mapper.Map<UpdateCustomerResponse>(customer);
    }
}
