using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderService.DTOs;
using OrderService.Infrastructure.Database;

namespace OrderService.Features.CreateOrder
{
    public record GetOrderStatusQuery(int OrderId) : IRequest<OrderStatusResponse?>;

    public class GetOrderStatusQueryHandler : IRequestHandler<GetOrderStatusQuery, OrderStatusResponse?>
    {
        private readonly OrderDbContext _dbContext;

        public GetOrderStatusQueryHandler(OrderDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OrderStatusResponse?> Handle(GetOrderStatusQuery query, CancellationToken cancellationToken)
        {
            var order = await _dbContext.Orders
                .FirstOrDefaultAsync(o => o.OrderId == query.OrderId, cancellationToken);

            if (order == null)
            {
                return null;
            }

            var sagaState = await _dbContext.SagaStates
                .FirstOrDefaultAsync(s => s.OrderId == query.OrderId, cancellationToken);

            return new OrderStatusResponse
            {
                OrderId = order.OrderId,
                CustomerId = order.CustomerId,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                OrderDate = order.OrderDate,
                SagaStatus = sagaState != null ? new SagaStatusDto
                {
                    SagaId = sagaState.SagaId,
                    CurrentStep = sagaState.CurrentStep,
                    IsOrderDetailsCompleted = sagaState.IsOrderDetailsCompleted,
                    IsPaymentCompleted = sagaState.IsPaymentCompleted,
                    IsNotificationCompleted = sagaState.IsNotificationCompleted,
                    ErrorMessage = sagaState.ErrorMessage
                } : null
            };
        }
    }
}
