namespace OrderDetailsService.Consumers;

using Shared.Messages.Events;
using OrderDetailsService.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

public class OrderCancelledConsumer
{
    private readonly OrderDetailsDbContext _context;
    private readonly ILogger<OrderCancelledConsumer> _logger;

    public OrderCancelledConsumer(
        OrderDetailsDbContext context,
        ILogger<OrderCancelledConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ConsumeAsync(OrderCancelledEvent @event)
    {
        _logger.LogInformation("Marking order details as cancelled for order {OrderId}", @event.OrderId);

        var orderDetails = await _context.OrderItems
            .Where(od => od.OrderId == @event.OrderId)
            .ToListAsync();

        foreach (var detail in orderDetails)
        {
            detail.Status = "Cancelled";
            detail.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Order details marked as cancelled for order {OrderId}", @event.OrderId);
    }
}
