using Microsoft.EntityFrameworkCore;
using MassTransit;
using OrderService.Infrastructure.Database;
using OrderService.Messages.Events;
using Shared.Messages;

namespace OrderService.Background
{
    public class OutboxDispatcher : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxDispatcher> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

        public OutboxDispatcher(IServiceProvider serviceProvider, ILogger<OutboxDispatcher> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OutboxDispatcher started - publishing OrderService outbox messages");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                    var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                    // OutboxDispatcher should ONLY handle messages meant for MassTransit in-memory publishing
                    // NOT messages meant for database outbox pattern (handled by OutboxConsumer or other services)
                    // Since we're using database outbox for all cross-service communication, this should be EMPTY
                    var allowedTypes = new string[] 
                    { 
                        // Empty - no messages should be published via MassTransit
                        // All commands/events use database outbox pattern
                    };

                    var pending = await db.OutboxMessages
                        .Where(o => !o.Processed)
                        .Where(o => allowedTypes.Contains(o.MessageType))  // Only process allowed types
                        .OrderBy(o => o.CreatedAt)
                        .Take(20)
                        .ToListAsync(stoppingToken);

                    if (pending.Any())
                    {
                        _logger.LogInformation("OutboxDispatcher: Found {Count} messages to publish via MassTransit", pending.Count);
                    }

                    foreach (var msg in pending)
                    {
                        try
                        {
                            if (msg.MessageType == nameof(OrderCreatedEvent))
                            {
                                var evt = System.Text.Json.JsonSerializer.Deserialize<OrderCreatedEvent>(msg.Payload);
                                if (evt != null)
                                {
                                    await publishEndpoint.Publish(evt, stoppingToken);
                                }
                            }

                            msg.Processed = true;
                            msg.ProcessedAt = DateTime.UtcNow;
                            db.OutboxMessages.Update(msg);
                            await db.SaveChangesAsync(stoppingToken);

                            _logger.LogInformation("OutboxDispatcher: Published outbox message {Id} of type {Type}", msg.Id, msg.MessageType);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to dispatch outbox message {Id}", msg.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OutboxDispatcher error");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("OutboxDispatcher stopping");
        }
    }
}
