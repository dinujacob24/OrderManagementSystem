using CustomerService.Common.Enums;
using CustomerService.Common.Exceptions;
using CustomerService.Common.Persistence;
using CustomerService.Infrastructure.OrderClient;
using MediatR;

namespace CustomerService.Features.DeactivateCustomer;

public class DeactivateCustomerHandler : IRequestHandler<DeactivateCustomerCommand, DeactivateCustomerResponse>
{
    private readonly CustomerDbContext _db;
    private readonly IOrderServiceClient _orderServiceClient;
    private readonly ILogger<DeactivateCustomerHandler> _logger;

    public DeactivateCustomerHandler(
        CustomerDbContext db,
        IOrderServiceClient orderServiceClient,
        ILogger<DeactivateCustomerHandler> logger)
    {
        _db = db;
        _orderServiceClient = orderServiceClient;
        _logger = logger;
    }

    public async Task<DeactivateCustomerResponse> Handle(
        DeactivateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await _db.Customers.FindAsync(new object[] { cmd.CustomerId }, ct)
            ?? throw new NotFoundException("Customer", cmd.CustomerId);

        if (customer.Status != CustomerStatus.Inactive)
        {
            // Deactivate the customer
            customer.Status = CustomerStatus.Inactive;
            customer.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Customer {CustomerId} deactivated", cmd.CustomerId);

            // Cancel all pending/active orders for this customer
            var cancellationResult = await _orderServiceClient.CancelCustomerOrdersAsync(
                cmd.CustomerId,
                "Customer account deactivated",
                ct);

            if (cancellationResult.Success)
            {
                _logger.LogInformation(
                    "Cancelled {Count} orders for deactivated customer {CustomerId}",
                    cancellationResult.CancelledOrdersCount,
                    cmd.CustomerId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to cancel orders for customer {CustomerId}: {Error}. Customer was still deactivated.",
                    cmd.CustomerId,
                    cancellationResult.ErrorMessage);
                // Note: We don't throw exception here - customer deactivation succeeds even if order cancellation fails
                // Orders will be rejected if customer tries to place new ones due to validation
            }
        }

        return new DeactivateCustomerResponse(customer.CustomerId, customer.Status, customer.UpdatedAt);
    }
}
