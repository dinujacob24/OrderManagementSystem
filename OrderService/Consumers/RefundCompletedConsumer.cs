namespace OrderService.Consumers;

using Shared.Messages.Events;
using OrderService.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

public class RefundCompletedConsumer
{
    private readonly OrderDbContext _context;
    private readonly ILogger<RefundCompletedConsumer> _logger;

    public RefundCompletedConsumer(
        OrderDbContext context,
        ILogger<RefundCompletedConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ConsumeAsync(RefundCompletedEvent @event)
    {
        _logger.LogInformation("Processing RefundCompleted for order {OrderId}", @event.OrderId);

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.OrderId == @event.OrderId);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", @event.OrderId);
            return;
        }

        if (@event.Success)
        {
            order.MarkAsRefunded();
            _logger.LogInformation("Order {OrderId} marked as refunded", @event.OrderId);
        }
        else
        {
            _logger.LogError("Refund failed for order {OrderId}: {Message}", 
                @event.OrderId, @event.Message);
            // Handle refund failure - keep in Cancelled state
        }

        await _context.SaveChangesAsync();
    }
}
