using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;
using OrderService.DTOs;
using OrderService.Infrastructure.Database;
using OrderService.Messages.Events;
using Shared.Messages;
using Shared.Messages.Commands;
using Shared.Messages.Events;
using NotificationCompletedEvent = Shared.Messages.Events.NotificationCompletedEvent;
using SharedOrderCreatedEvent = Shared.Messages.Events.OrderCreatedEvent;
using SharedOrderItemEventDto = Shared.Messages.Events.OrderItemEventDto;

namespace OrderService.Saga
{
    public class OrderSagaOrchestrator
    {
        private readonly OrderDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<OrderSagaOrchestrator> _logger;

        public OrderSagaOrchestrator(
            OrderDbContext dbContext,
            IPublishEndpoint publishEndpoint,
            ILogger<OrderSagaOrchestrator> logger)
        {
            _dbContext = dbContext;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task<Guid> StartOrderSaga(Order order, List<Shared.Messages.Events.OrderItemDto> items)
        {
            var sagaId = Guid.NewGuid();

            var sagaState = new SagaState
            {
                SagaId = sagaId,
                OrderId = order.OrderId,
                CustomerId = order.CustomerId,
                CurrentStep = "OrderCreated",
                Status = OrderStatus.Pending,
                StartedAt = DateTime.UtcNow,
                RetryCount = 0
            };

            _dbContext.SagaStates.Add(sagaState);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Saga {SagaId} started for Order {OrderId}", sagaId, order.OrderId);

            // Save OrderCreatedEvent to outbox for reliable delivery to OrderDetailsService
            var orderCreatedEvent = new SharedOrderCreatedEvent
            {
                SagaId = sagaId,
                OrderId = order.OrderId,
                CustomerId = order.CustomerId,
                Items = items.Select(i => new SharedOrderItemEventDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList(),
                TotalAmount = order.TotalAmount,
                Timestamp = DateTime.UtcNow
            };

            var outbox = new Infrastructure.Database.OutboxMessage
            {
                MessageType = nameof(Shared.Messages.Events.OrderCreatedEvent),
                Payload = System.Text.Json.JsonSerializer.Serialize(orderCreatedEvent),
                CreatedAt = DateTime.UtcNow,
                Processed = false
            };

            _dbContext.OutboxMessages.Add(outbox);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("OrderCreatedEvent saved to outbox for Saga {SagaId}", sagaId);

            // Update order status
            order.Status = OrderStatus.OrderDetailsProcessing;
            await _dbContext.SaveChangesAsync();

            return sagaId;
        }

        public async Task HandleOrderDetailsCompleted(Shared.Messages.Events.OrderDetailsCompletedEvent @event)
        {
            _logger.LogInformation("HandleOrderDetailsCompleted called - SagaId: {SagaId}, OrderId: {OrderId}, Success: {Success}",
                @event.SagaId, @event.OrderId, @event.Success);

            var sagaState = await _dbContext.SagaStates
                .FirstOrDefaultAsync(s => s.SagaId == @event.SagaId);

            if (sagaState == null)
            {
                _logger.LogWarning("Saga {SagaId} not found", @event.SagaId);
                return;
            }

            _logger.LogInformation("Found saga state - Current step: {CurrentStep}, IsOrderDetailsCompleted: {IsCompleted}",
                sagaState.CurrentStep, sagaState.IsOrderDetailsCompleted);

            if (@event.Success)
            {
                _logger.LogInformation("Event Success=true, updating saga state...");

                sagaState.IsOrderDetailsCompleted = true;
                sagaState.CurrentStep = "OrderDetailsCompleted";

                var order = await _dbContext.Orders.FindAsync(@event.OrderId);
                if (order != null)
                {
                    order.Status = OrderStatus.PaymentProcessing;
                    order.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation("Updated order {OrderId} status to PaymentProcessing", order.OrderId);
                }

                _logger.LogInformation("Order details completed for Saga {SagaId}, initiating payment", @event.SagaId);

                // Send command to Payment Service via database outbox
                var paymentCommand = new Shared.Messages.Commands.ProcessPaymentCommand
                {
                    SagaId = @event.SagaId,
                    OrderId = @event.OrderId,
                    CustomerId = sagaState.CustomerId,
                    Amount = order?.TotalAmount ?? 0,
                    Timestamp = DateTime.UtcNow
                };

                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = nameof(Shared.Messages.Commands.ProcessPaymentCommand),
                    Payload = System.Text.Json.JsonSerializer.Serialize(paymentCommand),
                    CreatedAt = DateTime.UtcNow,
                    Processed = false
                };

                _dbContext.OutboxMessages.Add(outboxMessage);

                // Don't call SaveChangesAsync here - let the calling code handle it within transaction
                _logger.LogInformation("ProcessPaymentCommand prepared for outbox for OrderId: {OrderId}", @event.OrderId);
            }
            else
            {
                _logger.LogError("Order details failed for Saga {SagaId}: {Error}", @event.SagaId, @event.ErrorMessage);
                await CompensateOrder(sagaState, "Order details processing failed: " + @event.ErrorMessage);
            }
        }

        public async Task HandlePaymentCompleted(Shared.Messages.Events.PaymentCompletedEvent @event)
        {
            _logger.LogInformation("HandlePaymentCompleted called - SagaId: {SagaId}, OrderId: {OrderId}, Success: {Success}",
                @event.SagaId, @event.OrderId, @event.Success);

            var sagaState = await _dbContext.SagaStates
                .FirstOrDefaultAsync(s => s.SagaId == @event.SagaId);

            if (sagaState == null)
            {
                _logger.LogWarning("Saga {SagaId} not found", @event.SagaId);
                return;
            }

            _logger.LogInformation("Found saga state - Current step: {CurrentStep}, IsPaymentCompleted: {IsCompleted}",
                sagaState.CurrentStep, sagaState.IsPaymentCompleted);

            if (@event.Success)
            {
                _logger.LogInformation("Payment Success=true, updating saga state...");

                sagaState.IsPaymentCompleted = true;
                sagaState.CurrentStep = "PaymentCompleted";

                var order = await _dbContext.Orders.FindAsync(@event.OrderId);
                if (order != null)
                {
                    order.Status = OrderStatus.NotificationProcessing;
                    order.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation("Updated order {OrderId} status to NotificationProcessing", order.OrderId);
                }

                // Don't call SaveChangesAsync here - let the calling code handle it within transaction
                _logger.LogInformation("Payment saga state updated, ready to commit transaction");

                _logger.LogInformation("Payment completed for Saga {SagaId}, sending notification", @event.SagaId);

                // Send command to Notification Service via database outbox
                var notificationCommand = new Shared.Messages.Commands.SendNotificationCommand
                {
                    SagaId = @event.SagaId,
                    OrderId = @event.OrderId,
                    CustomerId = sagaState.CustomerId,
                    Message = $"Your order #{@event.OrderId} has been successfully placed!",
                    NotificationType = "OrderConfirmation",
                    Timestamp = DateTime.UtcNow
                };

                var notificationOutbox = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = MessageTypes.SendNotificationCommand,
                    Payload = System.Text.Json.JsonSerializer.Serialize(notificationCommand),
                    CreatedAt = DateTime.UtcNow,
                    Processed = false
                };

                _dbContext.OutboxMessages.Add(notificationOutbox);
                _logger.LogInformation("SendNotificationCommand prepared for outbox for OrderId: {OrderId}", @event.OrderId);
            }
            else
            {
                _logger.LogError("Payment failed for Saga {SagaId}: {Error}", @event.SagaId, @event.ErrorMessage);
                await CompensatePayment(sagaState, "Payment processing failed: " + @event.ErrorMessage);
            }
        }

        public async Task HandleNotificationCompleted(NotificationCompletedEvent @event)
        {
            var sagaState = await _dbContext.SagaStates
                .FirstOrDefaultAsync(s => s.SagaId == @event.SagaId);

            if (sagaState == null)
            {
                _logger.LogWarning("Saga {SagaId} not found", @event.SagaId);
                return;
            }

            if (@event.Success)
            {
                sagaState.IsNotificationCompleted = true;
                sagaState.CurrentStep = "Completed";
                sagaState.Status = OrderStatus.Completed;
                sagaState.CompletedAt = DateTime.UtcNow;

                var order = await _dbContext.Orders.FindAsync(@event.OrderId);
                if (order != null)
                {
                    order.Status = OrderStatus.Completed;
                    order.UpdatedAt = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Saga {SagaId} completed successfully", @event.SagaId);

                // Publish final OrderCompletedEvent
                await _publishEndpoint.Publish(new OrderCompletedEvent
                {
                    SagaId = @event.SagaId,
                    OrderId = @event.OrderId,
                    CustomerId = sagaState.CustomerId,
                    TotalAmount = order?.TotalAmount ?? 0,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                // Notification failure - retry mechanism
                _logger.LogWarning("Notification failed for Saga {SagaId}, will retry", @event.SagaId);

                if (sagaState.RetryCount < 3)
                {
                    sagaState.RetryCount++;
                    await _dbContext.SaveChangesAsync();

                    // Retry notification
                    await _publishEndpoint.Publish(new SendNotificationCommand
                    {
                        SagaId = @event.SagaId,
                        OrderId = @event.OrderId,
                        CustomerId = sagaState.CustomerId,
                        Message = $"Your order #{@event.OrderId} has been successfully placed!",
                        NotificationType = "OrderConfirmation",
                        Timestamp = DateTime.UtcNow
                    });
                }
                else
                {
                    _logger.LogError("Notification failed after {RetryCount} retries for Saga {SagaId}", sagaState.RetryCount, @event.SagaId);
                    sagaState.CurrentStep = "NotificationFailed";
                    sagaState.ErrorMessage = "Notification failed after maximum retries";
                    sagaState.Status = OrderStatus.Completed; // Order is still valid
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        private async Task CompensateOrder(SagaState sagaState, string reason)
        {
            _logger.LogWarning("Compensating order for Saga {SagaId}", sagaState.SagaId);

            var order = await _dbContext.Orders.FindAsync(sagaState.OrderId);
            if (order != null)
            {
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;
            }

            sagaState.Status = OrderStatus.Cancelled;
            sagaState.CurrentStep = "Compensated";
            sagaState.ErrorMessage = reason;
            sagaState.CompletedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            // Notify customer about cancellation
            await _publishEndpoint.Publish(new OrderCancelledEvent
            {
                SagaId = sagaState.SagaId,
                OrderId = sagaState.OrderId,
                CustomerId = sagaState.CustomerId,
                Reason = reason,
                Timestamp = DateTime.UtcNow
            });

            await _publishEndpoint.Publish(new SendNotificationCommand
            {
                SagaId = sagaState.SagaId,
                OrderId = sagaState.OrderId,
                CustomerId = sagaState.CustomerId,
                Message = $"Your order #{sagaState.OrderId} has been cancelled. Reason: {reason}",
                NotificationType = "OrderCancellation",
                Timestamp = DateTime.UtcNow
            });

            _logger.LogInformation("Order cancelled and compensation completed for Saga {SagaId}", sagaState.SagaId);
        }

        private async Task CompensatePayment(SagaState sagaState, string reason)
        {
            _logger.LogWarning("Compensating payment for Saga {SagaId}", sagaState.SagaId);

            // In a real scenario, you would publish a rollback event to Order Details Service
            // to delete the order details that were created

            await CompensateOrder(sagaState, reason);
        }
    }
}
