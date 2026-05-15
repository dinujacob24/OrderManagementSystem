using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Database;
using OrderService.Saga;
using System.Text.Json;
using Shared.Messages;

namespace OrderService.Background
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
            _logger.LogInformation("OutboxConsumer started - polling OrderDetailsService outbox");
            _logger.LogInformation("OutboxConsumer: Polling interval = {Interval} seconds, Max attempts = {MaxAttempts}", _interval.TotalSeconds, _maxAttempts);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

                    // Create saga orchestrator manually and pass the same db context
                    var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<OrderSagaOrchestrator>>();
                    var sagaOrchestrator = new OrderSagaOrchestrator(db, publishEndpoint, logger);

                    var now = DateTime.UtcNow;
                    var lockToken = Guid.NewGuid();
                    var lockExpiry = now.Add(_lockDuration);

                    // Log polling activity
                    var totalUnprocessed = await db.OutboxMessages
                        .Where(m => (m.MessageType == MessageTypes.OrderDetailsCompletedEvent 
                                  || m.MessageType == MessageTypes.OrderDetailsFailedEvent
                                  || m.MessageType == MessageTypes.PaymentCompletedEvent) 
                                  && !m.Processed)
                        .CountAsync(stoppingToken);

                    // Debug: Check what's actually in the database
                    var allMessages = await db.OutboxMessages
                        .OrderByDescending(m => m.CreatedAt)
                        .Take(10)
                        .Select(m => new { m.Id, m.MessageType, m.Processed, m.CreatedAt })
                        .ToListAsync(stoppingToken);

                    _logger.LogInformation("DEBUG: Total messages in DB (last 10): {Count}", allMessages.Count);
                    foreach (var m in allMessages)
                    {
                        _logger.LogInformation("DEBUG:   - Id: {Id}, Type: '{MessageType}', Processed: {Processed}, Created: {Created}", 
                            m.Id, m.MessageType, m.Processed, m.CreatedAt);
                    }

                    _logger.LogInformation("DEBUG: Query returned {Count} unprocessed matching messages", totalUnprocessed);

                    if (totalUnprocessed > 0)
                    {
                        _logger.LogInformation("OutboxConsumer: Polling... Found {Count} unprocessed event messages", totalUnprocessed);
                    }

                    // Claim unprocessed messages atomically using a lock token
                    var claimQuery = @"
                        UPDATE OutboxMessages 
                        SET LockToken = {0}, LockExpiresAt = {1}, Attempts = Attempts + 1
                        WHERE Id IN (
                            SELECT Id FROM OutboxMessages 
                            WHERE Processed = 0 
                            AND (MessageType = '" + MessageTypes.OrderDetailsCompletedEvent + @"' 
                                 OR MessageType = '" + MessageTypes.OrderDetailsFailedEvent + @"'
                                 OR MessageType = '" + MessageTypes.PaymentCompletedEvent + @"')
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

                    foreach (var msg in claimedMessages)
                    {
                        try
                        {
                            _logger.LogInformation("OutboxConsumer: Processing message {Id} of type {Type}, Attempt {Attempt}",
                                msg.Id, msg.MessageType, msg.Attempts);

                            // Use explicit transaction to ensure atomicity
                            await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

                            try
                            {
                                // Process based on message type
                                if (msg.MessageType == MessageTypes.OrderDetailsCompletedEvent)
                                {
                                var evt = JsonSerializer.Deserialize<OrderDetailsCompletedEventDto>(msg.Payload);
                                if (evt != null)
                                {
                                    _logger.LogInformation("OutboxConsumer: Deserialized OrderDetailsCompletedEvent - SagaId: {SagaId}, OrderId: {OrderId}, Success: {Success}",
                                        evt.SagaId, evt.OrderId, evt.Success);

                                    // Map to local event shape and process via saga
                                    var localEvent = new Shared.Messages.Events.OrderDetailsCompletedEvent
                                    {
                                        SagaId = evt.SagaId,
                                        OrderId = evt.OrderId,
                                        Success = evt.Success,
                                        ErrorMessage = evt.ErrorMessage,
                                        Timestamp = evt.Timestamp
                                    };

                                            await sagaOrchestrator.HandleOrderDetailsCompleted(localEvent);

                                            _logger.LogInformation("OutboxConsumer: HandleOrderDetailsCompleted returned successfully");
                                        }
                                    }
                            else if (msg.MessageType == "OrderDetailsFailedEvent")
                            {
                                var evt = JsonSerializer.Deserialize<OrderDetailsFailedEventDto>(msg.Payload);
                                if (evt != null)
                                {
                                    // Map to local event shape
                                    var localEvent = new Shared.Messages.Events.OrderDetailsCompletedEvent
                                    {
                                        SagaId = evt.SagaId,
                                        OrderId = evt.OrderId,
                                        Success = false,
                                        ErrorMessage = evt.Reason,
                                        Timestamp = evt.Timestamp
                                    };

                                    await sagaOrchestrator.HandleOrderDetailsCompleted(localEvent);
                                }
                            }
                            else if (msg.MessageType == "PaymentCompletedEvent")
                            {
                                var evt = JsonSerializer.Deserialize<PaymentCompletedEventDto>(msg.Payload);
                                if (evt != null)
                                {
                                    _logger.LogInformation("OutboxConsumer: Deserialized PaymentCompletedEvent - SagaId: {SagaId}, OrderId: {OrderId}, Success: {Success}",
                                        evt.SagaId, evt.OrderId, evt.Success);

                                    var localEvent = new Shared.Messages.Events.PaymentCompletedEvent
                                    {
                                        SagaId = evt.SagaId,
                                        OrderId = evt.OrderId,
                                        Amount = evt.Amount,
                                        TransactionId = evt.TransactionId,
                                        Success = evt.Success,
                                        ErrorMessage = evt.ErrorMessage,
                                        Timestamp = evt.Timestamp
                                    };

                                    await sagaOrchestrator.HandlePaymentCompleted(localEvent);

                                    _logger.LogInformation("OutboxConsumer: HandlePaymentCompleted returned");
                                }
                            }

                            // Mark as processed
                            msg.Processed = true;
                            msg.ProcessedAt = DateTime.UtcNow;
                            msg.LockToken = null;
                            msg.LockExpiresAt = null;
                            msg.LastError = null;

                            db.OutboxMessages.Update(msg);
                            await db.SaveChangesAsync(stoppingToken);

                            // Commit the entire transaction (saga state + outbox message)
                            await transaction.CommitAsync(stoppingToken);

                            _logger.LogInformation("OutboxConsumer: Successfully processed message {Id} and committed transaction", msg.Id);
                            }
                            catch (Exception innerEx)
                            {
                                // Rollback transaction on any error
                                await transaction.RollbackAsync(stoppingToken);
                                _logger.LogError(innerEx, "OutboxConsumer: Transaction rolled back for message {Id}", msg.Id);
                                throw; // Re-throw to outer catch
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "OutboxConsumer: Failed to process message {Id}, Attempt {Attempt}",
                                msg.Id, msg.Attempts);

                            // Record error and release lock
                            msg.LastError = ex.Message;
                            msg.LockToken = null;
                            msg.LockExpiresAt = null;

                            // If max attempts reached, mark as processed (dead-letter)
                            if (msg.Attempts >= _maxAttempts)
                            {
                                _logger.LogError("OutboxConsumer: Message {Id} exceeded max attempts ({MaxAttempts}), marking as dead-letter",
                                    msg.Id, _maxAttempts);
                                msg.Processed = true; // Mark as processed to stop retries
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

        // DTOs for deserialization from OrderDetailsService events
        private class OrderDetailsCompletedEventDto
        {
            public Guid SagaId { get; set; }
            public int OrderId { get; set; }
            public List<OrderItemDto> Items { get; set; } = new();
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class OrderDetailsFailedEventDto
        {
            public Guid SagaId { get; set; }
            public int OrderId { get; set; }
            public string Reason { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }

        private class PaymentCompletedEventDto
        {
            public Guid SagaId { get; set; }
            public int OrderId { get; set; }
            public decimal Amount { get; set; }
            public string? TransactionId { get; set; }
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class OrderItemDto
        {
            public int OrderItemId { get; set; }
            public string ProductId { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal TotalPrice { get; set; }
        }
    }
}
