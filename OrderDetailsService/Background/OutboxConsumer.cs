using Microsoft.EntityFrameworkCore;
using OrderDetailsService.Infrastructure.Database;
using System.Text.Json;
using Shared.Messages.Events;
using SharedOrderCreatedEvent = Shared.Messages.Events.OrderCreatedEvent;

namespace OrderDetailsService.Background
{
    public class OutboxConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxConsumer> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
        private readonly int _maxAttempts = 5;
        private readonly TimeSpan _lockDuration = TimeSpan.FromMinutes(5);

        public OutboxConsumer(IServiceProvider serviceProvider, ILogger<OutboxConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxConsumer started - polling OrderService outbox for OrderCreatedEvent");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<OrderDetailsDbContext>();
                    var orderCreatedConsumer = scope.ServiceProvider.GetRequiredService<Consumers.OrderCreatedConsumer>();

                    var now = DateTime.UtcNow;
                    var lockToken = Guid.NewGuid();
                    var lockExpiry = now.Add(_lockDuration);

                    // Claim unprocessed OrderCreatedEvent messages atomically
                    var claimQuery = @"
                        UPDATE OutboxMessages 
                        SET LockToken = {0}, LockExpiresAt = {1}, Attempts = Attempts + 1
                        WHERE Id IN (
                            SELECT Id FROM OutboxMessages 
                            WHERE Processed = 0 
                            AND MessageType = 'OrderCreatedEvent'
                            AND Attempts < {2}
                            AND (LockExpiresAt IS NULL OR LockExpiresAt < {3})
                            ORDER BY CreatedAt
                            LIMIT 20
                        )";

                    await db.Database.ExecuteSqlRawAsync(
                        claimQuery,
                        cancellationToken: stoppingToken,
                        parameters: new object[] { lockToken, lockExpiry, _maxAttempts, now });

                    // Fetch claimed messages
                    var claimedMessages = await db.OutboxMessages
                        .Where(o => o.LockToken == lockToken)
                        .ToListAsync(stoppingToken);

                    if (claimedMessages.Any())
                    {
                        _logger.LogInformation("OutboxConsumer: Found {Count} OrderCreatedEvent messages to process", claimedMessages.Count);
                    }

                    foreach (var msg in claimedMessages)
                    {
                        try
                        {
                            _logger.LogInformation("OutboxConsumer: Processing OrderCreatedEvent {Id}, Attempt {Attempt}",
                                msg.Id, msg.Attempts);

                            var evt = JsonSerializer.Deserialize<SharedOrderCreatedEvent>(msg.Payload);
                            if (evt != null)
                            {
                                // Process the event using the existing consumer logic
                                await ProcessOrderCreatedEvent(evt, scope);
                            }

                            // Mark as processed
                            msg.Processed = true;
                            msg.ProcessedAt = DateTime.UtcNow;
                            msg.LockToken = null;
                            msg.LockExpiresAt = null;
                            msg.LastError = null;

                            db.OutboxMessages.Update(msg);
                            await db.SaveChangesAsync(stoppingToken);

                            _logger.LogInformation("OutboxConsumer: Successfully processed OrderCreatedEvent {Id}", msg.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "OutboxConsumer: Failed to process message {Id}, Attempt {Attempt}",
                                msg.Id, msg.Attempts);

                            msg.LastError = ex.Message;
                            msg.LockToken = null;
                            msg.LockExpiresAt = null;

                            if (msg.Attempts >= _maxAttempts)
                            {
                                _logger.LogError("OutboxConsumer: Message {Id} exceeded max attempts, marking as dead-letter", msg.Id);
                                msg.Processed = true;
                                msg.ProcessedAt = DateTime.UtcNow;
                            }

                            db.OutboxMessages.Update(msg);
                            await db.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OutboxConsumer: Error in polling loop");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("OutboxConsumer stopping");
        }

        private async Task ProcessOrderCreatedEvent(OrderCreatedEvent message, IServiceScope scope)
        {
            var context = scope.ServiceProvider.GetRequiredService<OrderDetailsDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Consumers.OrderCreatedConsumer>>();

            logger.LogInformation("OrderDetailsService: Processing OrderCreated for OrderId: {OrderId}, SagaId: {SagaId}",
                message.OrderId, message.SagaId);

            try
            {
                if (message.Items == null || !message.Items.Any())
                {
                    logger.LogWarning("OrderDetailsService: No items found for OrderId: {OrderId}", message.OrderId);
                    await PublishFailureEvent(message, "No items to add", context);
                    return;
                }

                var existingItems = await context.OrderItems
                    .Where(oi => oi.OrderId == message.OrderId)
                    .ToListAsync();

                if (existingItems.Any())
                {
                    logger.LogWarning("OrderDetailsService: Items already exist for OrderId: {OrderId}", message.OrderId);
                    await PublishSuccessEvent(message, existingItems, context);
                    return;
                }

                var orderItems = message.Items.Select(item => new Domain.OrderItem
                {
                    OrderId = message.OrderId,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.Quantity * item.UnitPrice,
                    Status = Domain.OrderItemStatus.Added,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                context.OrderItems.AddRange(orderItems);

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
                    Success = true,
                    ErrorMessage = null,
                    Timestamp = DateTime.UtcNow
                };

                var outbox = new OutboxMessage
                {
                    MessageType = nameof(OrderDetailsCompletedEvent),
                    Payload = JsonSerializer.Serialize(outboxEvent),
                    CreatedAt = DateTime.UtcNow,
                    Processed = false
                };

                context.OutboxMessages.Add(outbox);
                await context.SaveChangesAsync();

                logger.LogInformation("OrderDetailsService: Successfully added {Count} items for OrderId: {OrderId}",
                    orderItems.Count, message.OrderId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OrderDetailsService: Failed to add items for OrderId: {OrderId}", message.OrderId);
                await PublishFailureEvent(message, ex.Message, context);
            }
        }

        private async Task PublishSuccessEvent(OrderCreatedEvent message, List<Domain.OrderItem> orderItems, OrderDetailsDbContext context)
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
                Success = true,
                ErrorMessage = null,
                Timestamp = DateTime.UtcNow
            };

            var outbox = new OutboxMessage
            {
                MessageType = nameof(OrderDetailsCompletedEvent),
                Payload = JsonSerializer.Serialize(completedEvent),
                CreatedAt = DateTime.UtcNow,
                Processed = false
            };

            context.OutboxMessages.Add(outbox);
            await context.SaveChangesAsync();
        }

        private async Task PublishFailureEvent(OrderCreatedEvent message, string reason, OrderDetailsDbContext context)
        {
            var failedEvent = new OrderDetailsFailedEvent
            {
                SagaId = message.SagaId,
                OrderId = message.OrderId,
                Reason = reason,
                Timestamp = DateTime.UtcNow
            };

            var outbox = new OutboxMessage
            {
                MessageType = nameof(OrderDetailsFailedEvent),
                Payload = JsonSerializer.Serialize(failedEvent),
                CreatedAt = DateTime.UtcNow,
                Processed = false
            };

            context.OutboxMessages.Add(outbox);
            await context.SaveChangesAsync();
        }
    }
}
