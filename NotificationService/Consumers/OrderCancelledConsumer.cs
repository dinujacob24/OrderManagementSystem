namespace NotificationService.Consumers;

using Shared.Messages.Events;
using NotificationService.Infrastructure.Database;
using NotificationService.Domain;

public class OrderCancelledConsumer
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<OrderCancelledConsumer> _logger;

    public OrderCancelledConsumer(
        NotificationDbContext context,
        ILogger<OrderCancelledConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ConsumeAsync(OrderCancelledEvent @event)
    {
        _logger.LogInformation("Sending cancellation notification for order {OrderId}", @event.OrderId);

        var notification = new Notification
        {
            OrderId = @event.OrderId,
            CustomerId = @event.CustomerId,
            NotificationType = "Email",
            Status = "Sent",
            Message = $"Your order {@event.OrderId} has been cancelled. Reason: {@event.Reason}",
            CreatedAt = DateTime.UtcNow,
            SentAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cancellation notification sent for order {OrderId}", @event.OrderId);
    }
}
