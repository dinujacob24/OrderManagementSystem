using CustomerService.Common.Enums;
using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using MediatR;

namespace CustomerService.Features.ReactivateCustomer;

public class ReactivateCustomerHandler : IRequestHandler<ReactivateCustomerCommand, ReactivateCustomerResponse>
{
    private readonly CustomerDbContext _db;

    public ReactivateCustomerHandler(CustomerDbContext db)
    {
        _db = db;
    }

    public async Task<ReactivateCustomerResponse> Handle(
        ReactivateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { cmd.CustomerId }, ct)
            ?? throw new NotFoundException("Customer", cmd.CustomerId);

        if (customer.Status == CustomerStatus.Suspended)
        {
            throw new ConflictException(
                $"Customer '{cmd.CustomerId}' is suspended and cannot be reactivated via this endpoint.");
        }

        if (customer.Status != CustomerStatus.Active)
        {
            customer.Status = CustomerStatus.Active;
            customer.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return new ReactivateCustomerResponse(customer.CustomerId, customer.Status, customer.UpdatedAt);
    }
}
