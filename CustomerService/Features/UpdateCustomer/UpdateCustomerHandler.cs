using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerService.Features.UpdateCustomer;

public class UpdateCustomerHandler : IRequestHandler<UpdateCustomerCommand, UpdateCustomerResponse>
{
    private readonly CustomerDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateCustomerHandler> _logger;

    public UpdateCustomerHandler(CustomerDbContext db, IMapper mapper, ILogger<UpdateCustomerHandler> logger)
    {
        _db = db;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UpdateCustomerResponse> Handle(
        UpdateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { cmd.CustomerId }, ct);

        if (customer is null)
        {
            _logger.LogWarning("Update failed — customer {CustomerId} not found", cmd.CustomerId);
            throw new NotFoundException("Customer", cmd.CustomerId);
        }

        customer.FirstName = cmd.Request.FirstName;
        customer.LastName = cmd.Request.LastName;
        customer.Email = cmd.Request.Email;
        customer.Phone = cmd.Request.Phone;
        customer.AddressLine1 = cmd.Request.AddressLine1;
        customer.City = cmd.Request.City;
        customer.Country = cmd.Request.Country;
        customer.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated customer {CustomerId}", cmd.CustomerId);

        return _mapper.Map<UpdateCustomerResponse>(customer);
    }
}
