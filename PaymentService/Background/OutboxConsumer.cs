using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure.Database;
using PaymentService.Domain;
using System.Text.Json;
using Shared.Messages.Events;

namespace PaymentService.Background
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
            _logger.LogInformation("PaymentService OutboxConsumer started - polling for ProcessPaymentCommand");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

                    var now = DateTime.UtcNow;
                    var lockToken = Guid.NewGuid();
                    var lockExpiry = now.Add(_lockDuration);

                    // Check for unprocessed payment commands
                    var totalUnprocessed = await db.OutboxMessages
                        .Where(m => m.MessageType == "ProcessPaymentCommand" && !m.Processed)
                        .CountAsync(stoppingToken);

                    if (totalUnprocessed > 0)
                    {
                        _logger.LogInformation("PaymentService: Found {Count} unprocessed payment commands", totalUnprocessed);
                    }

                    // Claim messages atomically
                    var claimQuery = @"
                        UPDATE OutboxMessages 
                        SET LockToken = {0}, LockExpiresAt = {1}, Attempts = Attempts + 1
                        WHERE Id IN (
                            SELECT Id FROM OutboxMessages 
                            WHERE Processed = 0 
                            AND MessageType = 'ProcessPaymentCommand'
                            AND Attempts < {2}
                            AND (LockExpiresAt IS NULL OR LockExpiresAt < {3})
                            ORDER BY CreatedAt
                            LIMIT 20
                        )";

                    await db.Database.ExecuteSqlRawAsync(
                        claimQuery,
                        cancellationToken: stoppingToken,
                        parameters: new object[] { lockToken, lockExpiry, _maxAttempts, now });

                    var claimedMessages = await db.OutboxMessages
                        .Where(o => o.LockToken == lockToken)
                        .ToListAsync(stoppingToken);

                    foreach (var msg in claimedMessages)
                    {
                        try
                        {
                            _logger.LogInformation("PaymentService: Processing payment command {Id}", msg.Id);

                            var command = JsonSerializer.Deserialize<ProcessPaymentCommandDto>(msg.Payload);
                            if (command != null)
                            {
                                await ProcessPayment(db, command, stoppingToken);
                            }

                            // Mark as processed
                            msg.Processed = true;
                            msg.ProcessedAt = DateTime.UtcNow;
                            msg.LockToken = null;
                            msg.LockExpiresAt = null;
                            msg.LastError = null;

                            db.OutboxMessages.Update(msg);
                            await db.SaveChangesAsync(stoppingToken);

                            _logger.LogInformation("PaymentService: Successfully processed command {Id}", msg.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "PaymentService: Failed to process command {Id}", msg.Id);

                            msg.LastError = ex.Message;
                            msg.LockToken = null;
                            msg.LockExpiresAt = null;

                            if (msg.Attempts >= _maxAttempts)
                            {
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
                    _logger.LogError(ex, "PaymentService: Error in polling loop");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task ProcessPayment(PaymentDbContext db, ProcessPaymentCommandDto command, CancellationToken cancellationToken)
        {
            _logger.LogInformation("PaymentService: Processing payment for OrderId: {OrderId}, Amount: {Amount}",
                command.OrderId, command.Amount);

            // Create payment record
            var payment = new Payment
            {
                SagaId = command.SagaId,
                OrderId = command.OrderId,
                CustomerId = command.CustomerId,
                Amount = command.Amount,
                Status = "Processing",
                CreatedAt = DateTime.UtcNow
            };

            db.Payments.Add(payment);
            await db.SaveChangesAsync(cancellationToken);

            // Simulate payment processing with configurable success/failure
            var (success, errorMessage) = SimulatePaymentProcessing(command);

            payment.Status = success ? "Completed" : "Failed";
            payment.ProcessedAt = DateTime.UtcNow;
            payment.ErrorMessage = errorMessage;
            payment.TransactionId = success ? $"TXN-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}" : null;
            payment.PaymentMethod = "SimulatedPayment";

            // Create payment completed/failed event
            var paymentEvent = new PaymentCompletedEvent
            {
                SagaId = command.SagaId,
                OrderId = command.OrderId,
                Amount = command.Amount,
                Success = success,
                ErrorMessage = errorMessage,
                TransactionId = payment.TransactionId,
                Timestamp = DateTime.UtcNow
            };

            var outbox = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = nameof(PaymentCompletedEvent),
                Payload = JsonSerializer.Serialize(paymentEvent),
                CreatedAt = DateTime.UtcNow,
                Processed = false
            };

            db.OutboxMessages.Add(outbox);
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("PaymentService: Payment {Status} for OrderId: {OrderId}, TransactionId: {TxnId}",
                payment.Status, command.OrderId, payment.TransactionId ?? "N/A");
        }

        private (bool success, string? errorMessage) SimulatePaymentProcessing(ProcessPaymentCommandDto command)
        {
            // Simulate payment scenarios based on amount or customer ID

            // Rule 1: Amounts ending in .99 fail (insufficient funds)
            if (command.Amount % 1 == 0.99m)
            {
                return (false, "Insufficient funds");
            }

            // Rule 2: CustomerId containing "fail" fails (card declined)
            if (command.CustomerId.Contains("fail", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Payment card declined");
            }

            // Rule 3: Amounts over 10000 fail (transaction limit exceeded)
            if (command.Amount > 10000)
            {
                return (false, "Transaction amount exceeds limit");
            }

            // Rule 4: Random failure rate (DISABLED for stable testing)
            // Uncomment to enable random failures for testing error handling
            // if (Random.Shared.Next(100) < 10)
            // {
            //     return (false, "Payment gateway timeout");
            // }

            // Otherwise succeed
            return (true, null);
        }

        private class ProcessPaymentCommandDto
        {
            public Guid SagaId { get; set; }
            public int OrderId { get; set; }
            public string CustomerId { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
