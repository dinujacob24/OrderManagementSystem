namespace OrderService.Features.CancelOrder;

using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;
using OrderService.Infrastructure.Database;
using OrderService.Saga;
using Shared.Messages.Events;

public class CancelOrderCommand : IRequest<CancelOrderResponse>
{
    public int OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResponse>
{
    private readonly OrderDbContext _dbContext;
    private readonly OrderSagaOrchestrator _sagaOrchestrator;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        OrderDbContext dbContext,
        OrderSagaOrchestrator sagaOrchestrator,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _dbContext = dbContext;
        _sagaOrchestrator = sagaOrchestrator;
        _logger = logger;
    }

    public async Task<CancelOrderResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FindAsync(request.OrderId);
        if (order == null)
        {
            return new CancelOrderResponse { Success = false, Message = "Order not found." };
        }

        // Mark order as cancelled
        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Find saga state
        var sagaState = await _dbContext.SagaStates.FirstOrDefaultAsync(s => s.OrderId == order.OrderId);
        var sagaId = sagaState?.SagaId ?? Guid.NewGuid();

        // Publish OrderCancelledEvent and trigger saga compensation
        var orderCancelledEvent = new OrderCancelledEvent
        {
            OrderId = order.OrderId,
            CustomerId = order.CustomerId,
            SagaId = sagaId,
            Reason = "Order cancelled by user"
        };
        await _sagaOrchestrator.HandleOrderCancelledEvent(orderCancelledEvent);

        return new CancelOrderResponse { Success = true, Message = "Order cancelled successfully." };
    }
}
