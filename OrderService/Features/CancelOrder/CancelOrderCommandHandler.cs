namespace OrderService.Features.CancelOrder;

using MediatR;
using OrderService.Domain;
using OrderService.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Shared.Messages.Events;

public class CancelOrderCommand : IRequest<CancelOrderResponse>
{
    public int OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResponse>
{
    private readonly OrderDbContext _context;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        OrderDbContext context,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CancelOrderResponse> Handle(
        CancelOrderCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId, cancellationToken);

            if (order == null)
            {
                return new CancelOrderResponse
                {
                    Success = false,
                    Message = "Order not found",
                    OrderId = request.OrderId
                };
            }

            // Cancel the order
            order.Cancel(request.Reason);

            // Publish OrderCancelledEvent to Outbox
            var orderCancelledEvent = new OrderCancelledEvent
            {
                OrderId = order.OrderId,
                CustomerId = order.CustomerId,
                Reason = request.Reason,
                CancelledAt = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid()
            };

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = "order-cancelled",
                Payload = System.Text.Json.JsonSerializer.Serialize(orderCancelledEvent),
                CreatedAt = DateTime.UtcNow
            };

            _context.OutboxMessages.Add(outboxMessage);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} cancelled successfully", order.OrderId);

            return new CancelOrderResponse
            {
                Success = true,
                Message = "Order cancelled successfully",
                OrderId = order.OrderId
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot cancel order {OrderId}", request.OrderId);
            return new CancelOrderResponse
            {
                Success = false,
                Message = ex.Message,
                OrderId = request.OrderId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", request.OrderId);
            throw;
        }
    }
}
