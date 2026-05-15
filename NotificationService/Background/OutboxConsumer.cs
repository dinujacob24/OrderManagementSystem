using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Database;
using NotificationService.Domain;
using System.Text.Json;
using Shared.Messages;
using Shared.Messages.Events;

namespace NotificationService.Background
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
            _logger.LogInformation("NotificationService OutboxConsumer started - polling for SendNotificationCommand");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

                    var now = DateTime.UtcNow;
                    var lockToken = Guid.NewGuid();
                    var lockExpiry = now.Add(_lockDuration);

                    // Check for unprocessed notification commands
                    var totalUnprocessed = await db.OutboxMessages
                        .Where(m => m.MessageType == MessageTypes.SendNotificationCommand && !m.Processed)
                        .CountAsync(stoppingToken);

                    if (totalUnprocessed > 0)
                    {
                        _logger.LogInformation("NotificationService: Found {Count} unprocessed notification commands", totalUnprocessed);
                    }

                    // Claim messages atomically
                    var claimQuery = @"
                        UPDATE OutboxMessages 
                        SET LockToken = {0}, LockExpiresAt = {1}, Attempts = Attempts + 1
                        WHERE Id IN (
                            SELECT Id FROM OutboxMessages 
                            WHERE Processed = 0 
                            AND MessageType = '" + MessageTypes.SendNotificationCommand + @"'
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
                            _logger.LogInformation("NotificationService: Processing notification command {Id}", msg.Id);

                            var command = JsonSerializer.Deserialize<SendNotificationCommandDto>(msg.Payload);
                            if (command != null)
                            {
                                await SendNotification(db, command, stoppingToken);
                            }

                            // Mark as processed
                            msg.Processed = true;
                            msg.ProcessedAt = DateTime.UtcNow;
                            msg.LockToken = null;
                            msg.LockExpiresAt = null;
                            msg.LastError = null;

                            db.OutboxMessages.Update(msg);
                            await db.SaveChangesAsync(stoppingToken);

                            _logger.LogInformation("NotificationService: Successfully processed command {Id}", msg.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "NotificationService: Failed to process command {Id}", msg.Id);

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
                    _logger.LogError(ex, "NotificationService: Error in polling loop");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task SendNotification(NotificationDbContext db, SendNotificationCommandDto command, CancellationToken cancellationToken)
        {
            _logger.LogInformation("NotificationService: Sending notification for OrderId: {OrderId}, Type: {Type}",
                command.OrderId, command.NotificationType);

            // Create notification record
            var notification = new Notification
            {
                SagaId = command.SagaId,
                OrderId = command.OrderId,
                CustomerId = command.CustomerId,
                Message = command.Message,
                NotificationType = command.NotificationType,
                Status = "Sending",
                Recipient = $"{command.CustomerId}@example.com", // Mock email
                CreatedAt = DateTime.UtcNow
            };

            db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken);

            // Simulate notification sending (always succeeds for now)
            var (success, errorMessage) = SimulateNotificationSending(command);

            notification.Status = success ? "Sent" : "Failed";
            notification.SentAt = DateTime.UtcNow;
            notification.ErrorMessage = errorMessage;

            // Create notification completed event
            var notificationEvent = new NotificationCompletedEvent
            {
                SagaId = command.SagaId,
                OrderId = command.OrderId,
                Success = success,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow
            };

            var outbox = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = nameof(NotificationCompletedEvent),
                Payload = JsonSerializer.Serialize(notificationEvent),
                CreatedAt = DateTime.UtcNow,
                Processed = false
            };

            db.OutboxMessages.Add(outbox);
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("NotificationService: Notification {Status} for OrderId: {OrderId}",
                notification.Status, command.OrderId);
        }

        private (bool success, string? errorMessage) SimulateNotificationSending(SendNotificationCommandDto command)
        {
            // For mock purposes, always succeed
            // In real implementation, integrate with:
            // - SendGrid/AWS SES for email
            // - Twilio for SMS
            // - Firebase for push notifications

            _logger.LogInformation("Mock notification sent: {Message}", command.Message);
            return (true, null);
        }

        private class SendNotificationCommandDto
        {
            public Guid SagaId { get; set; }
            public int OrderId { get; set; }
            public string CustomerId { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string NotificationType { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
        }
    }
}
