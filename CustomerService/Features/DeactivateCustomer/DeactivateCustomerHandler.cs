using CustomerService.Common.Enums;
using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using MediatR;

namespace CustomerService.Features.DeactivateCustomer;

public class DeactivateCustomerHandler : IRequestHandler<DeactivateCustomerCommand, DeactivateCustomerResponse>
{
    private readonly CustomerDbContext _db;

    public DeactivateCustomerHandler(CustomerDbContext db)
    {
        _db = db;
    }

    public async Task<DeactivateCustomerResponse> Handle(
        DeactivateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { cmd.CustomerId }, ct)
            ?? throw new NotFoundException("Customer", cmd.CustomerId);

        if (customer.Status != CustomerStatus.Inactive)
        {
            customer.Status = CustomerStatus.Inactive;
            customer.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return new DeactivateCustomerResponse(customer.CustomerId, customer.Status, customer.UpdatedAt);
    }
}
