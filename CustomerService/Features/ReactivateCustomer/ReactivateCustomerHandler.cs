using CustomerService.Common.Enums;
using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CustomerService.Features.ReactivateCustomer;

public class ReactivateCustomerHandler : IRequestHandler<ReactivateCustomerCommand, ReactivateCustomerResponse>
{
    private readonly CustomerDbContext _db;
    private readonly ILogger<ReactivateCustomerHandler> _logger;

    public ReactivateCustomerHandler(CustomerDbContext db, ILogger<ReactivateCustomerHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ReactivateCustomerResponse> Handle(
        ReactivateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { cmd.CustomerId }, ct);

        if (customer is null)
        {
            _logger.LogWarning("Reactivate failed — customer {CustomerId} not found", cmd.CustomerId);
            throw new NotFoundException("Customer", cmd.CustomerId);
        }

        if (customer.Status == CustomerStatus.Suspended)
        {
            _logger.LogWarning(
                "Reactivate rejected — customer {CustomerId} is suspended",
                cmd.CustomerId);
            throw new ConflictException(
                $"Customer '{cmd.CustomerId}' is suspended and cannot be reactivated via this endpoint.");
        }

        if (customer.Status != CustomerStatus.Active)
        {
            customer.Status = CustomerStatus.Active;
            customer.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Reactivated customer {CustomerId}", cmd.CustomerId);
        }
        else
        {
            _logger.LogInformation(
                "Reactivate no-op — customer {CustomerId} already Active",
                cmd.CustomerId);
        }

        return new ReactivateCustomerResponse(customer.CustomerId, customer.Status, customer.UpdatedAt);
    }
}
