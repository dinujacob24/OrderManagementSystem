using MediatR;
using OrderService.Domain;
using OrderService.DTOs;
using OrderService.Infrastructure.Database;
using OrderService.Saga;

namespace OrderService.Features.CreateOrder
{
    public record CreateOrderCommand(
        string CustomerId,
        List<OrderItemCommand> Items
    ) : IRequest<CreateOrderResponse>;

    public record OrderItemCommand(
        string ProductId,
        string ProductName,
        int Quantity,
        decimal UnitPrice
    );

    public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
    {
        private readonly OrderDbContext _dbContext;
        private readonly OrderSagaOrchestrator _sagaOrchestrator;
        private readonly ILogger<CreateOrderCommandHandler> _logger;

        public CreateOrderCommandHandler(
            OrderDbContext dbContext,
            OrderSagaOrchestrator sagaOrchestrator,
            ILogger<CreateOrderCommandHandler> logger)
        {
            _dbContext = dbContext;
            _sagaOrchestrator = sagaOrchestrator;
            _logger = logger;
        }

        public async Task<CreateOrderResponse> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
        {
            // Validate command
            if (string.IsNullOrWhiteSpace(command.CustomerId))
            {
                throw new ArgumentException("CustomerId is required");
            }

            if (command.Items == null || !command.Items.Any())
            {
                throw new ArgumentException("At least one order item is required");
            }

            // Calculate total amount
            var totalAmount = command.Items.Sum(item => item.Quantity * item.UnitPrice);

            // Create order (without items - Order Details Service will handle items)
            var order = new Order
            {
                CustomerId = command.CustomerId,
                OrderDate = DateTime.UtcNow,
                TotalAmount = totalAmount,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} created for customer {CustomerId}", order.OrderId, order.CustomerId);

            // Map command items to DTOs for saga
            var itemsForSaga = command.Items.Select(item => new Shared.Messages.Events.OrderItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList();

            // Start Saga orchestration
            var sagaId = await _sagaOrchestrator.StartOrderSaga(order, itemsForSaga);

            return new CreateOrderResponse
            {
                OrderId = order.OrderId,
                SagaId = sagaId,
                CustomerId = order.CustomerId,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                OrderDate = order.OrderDate,
                Message = "Order created successfully and saga initiated"
            };
        }
    }
}
