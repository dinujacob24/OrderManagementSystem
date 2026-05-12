using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderDetailsService.Domain;
using OrderDetailsService.Infrastructure.Database;
using OrderDetailsService.Messages.Events;

namespace OrderDetailsService.Consumers
{
    public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
    {
        private readonly OrderDetailsDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<OrderCreatedConsumer> _logger;

        public OrderCreatedConsumer(
            OrderDetailsDbContext context,
            IPublishEndpoint publishEndpoint,
            ILogger<OrderCreatedConsumer> logger)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
        {
            var message = context.Message;
            _logger.LogInformation("OrderDetailsService: Processing OrderCreated for OrderId: {OrderId}, SagaId: {SagaId}",
                message.OrderId, message.SagaId);

            try
            {
                // Validate items
                if (message.Items == null || !message.Items.Any())
                {
                    _logger.LogWarning("OrderDetailsService: No items found for OrderId: {OrderId}", message.OrderId);
                    await PublishFailureEvent(message, "No items to add");
                    return;
                }

                // Check for duplicate items already added
                var existingItems = await _context.OrderItems
                    .Where(oi => oi.OrderId == message.OrderId)
                    .ToListAsync();

                if (existingItems.Any())
                {
                    _logger.LogWarning("OrderDetailsService: Items already exist for OrderId: {OrderId}", message.OrderId);
                    await PublishSuccessEvent(message, existingItems);
                    return;
                }

                // Add order items
                var orderItems = message.Items.Select(item => new OrderItem
                {
                    OrderId = message.OrderId,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.Quantity * item.UnitPrice,
                    Status = OrderItemStatus.Added,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _context.OrderItems.AddRange(orderItems);
                // Persist order items and the outbox message within the same transaction
                var outboxEvent = new OrderDetailsCompletedEvent
                {
                    SagaId = message.SagaId,
                    OrderId = message.OrderId,
                    Items = orderItems.Select(oi => new OrderItemDto
                    {
                        OrderItemId = oi.OrderItemId,
                        ProductId = oi.ProductId,
                        ProductName = oi.ProductName,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        TotalPrice = oi.TotalPrice
                    }).ToList(),
                    Timestamp = DateTime.UtcNow
                };

                var outbox = new Infrastructure.Database.OutboxMessage
                {
                    MessageType = nameof(OrderDetailsCompletedEvent),
                    Payload = System.Text.Json.JsonSerializer.Serialize(outboxEvent),
                    CreatedAt = DateTime.UtcNow,
                    Processed = false
                };

                _context.OutboxMessages.Add(outbox);
                await _context.SaveChangesAsync();

                _logger.LogInformation("OrderDetailsService: Successfully added {Count} items for OrderId: {OrderId}",
                    orderItems.Count, message.OrderId);

                // Publish success event (via outbox background processor)
                _logger.LogInformation("OrderDetailsService: Saved outbox message for OrderId: {OrderId}", message.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OrderDetailsService: Failed to add items for OrderId: {OrderId}", message.OrderId);
                await PublishFailureEvent(message, ex.Message);
            }
        }

        private async Task PublishSuccessEvent(OrderCreatedEvent message, List<OrderItem> orderItems)
        {
            var completedEvent = new OrderDetailsCompletedEvent
            {
                SagaId = message.SagaId,
                OrderId = message.OrderId,
                Items = orderItems.Select(oi => new OrderItemDto
                {
                    OrderItemId = oi.OrderItemId,
                    ProductId = oi.ProductId,
                    ProductName = oi.ProductName,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.TotalPrice
                }).ToList(),
                Timestamp = DateTime.UtcNow
            };

            await _publishEndpoint.Publish(completedEvent);
            _logger.LogInformation("OrderDetailsService: Published OrderDetailsCompletedEvent for OrderId: {OrderId}", message.OrderId);
        }

        private async Task PublishFailureEvent(OrderCreatedEvent message, string reason)
        {
            var failedEvent = new OrderDetailsFailedEvent
            {
                SagaId = message.SagaId,
                OrderId = message.OrderId,
                Reason = reason,
                Timestamp = DateTime.UtcNow
            };

            // Save failure event to outbox for reliable dispatch
            var outbox = new Infrastructure.Database.OutboxMessage
            {
                MessageType = nameof(OrderDetailsFailedEvent),
                Payload = System.Text.Json.JsonSerializer.Serialize(failedEvent),
                CreatedAt = DateTime.UtcNow,
                Processed = false
            };

            _context.OutboxMessages.Add(outbox);
            await _context.SaveChangesAsync();

            _logger.LogInformation("OrderDetailsService: Saved OrderDetailsFailedEvent to outbox for OrderId: {OrderId}, Reason: {Reason}",
                message.OrderId, reason);
        }
    }
}
