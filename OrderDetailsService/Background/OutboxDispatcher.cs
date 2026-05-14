using Microsoft.EntityFrameworkCore;
using MassTransit;
using OrderDetailsService.Infrastructure.Database;
using Shared.Messages.Events;
namespace OrderDetailsService.Background
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
            _logger.LogInformation("OutboxDispatcher started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<OrderDetailsDbContext>();
                    var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                    var pending = await db.OutboxMessages
                        .Where(o => !o.Processed)
                        .Where(o => o.MessageType == nameof(Shared.Messages.Events.OrderDetailsCompletedEvent) 
                                 || o.MessageType == nameof(OrderDetailsFailedEvent)) // Only process our own events
                        .OrderBy(o => o.CreatedAt)
                        .Take(20)
                        .ToListAsync(stoppingToken);

                    foreach (var msg in pending)
                    {
                        try
                        {
                            if (msg.MessageType == nameof(Shared.Messages.Events.OrderDetailsCompletedEvent))
                            {
                                var evt = System.Text.Json.JsonSerializer.Deserialize<Shared.Messages.Events.OrderDetailsCompletedEvent>(msg.Payload);
                                if (evt != null)
                                {
                                    await publishEndpoint.Publish(evt, stoppingToken);
                                }
                            }
                            else if (msg.MessageType == nameof(OrderDetailsFailedEvent))
                            {
                                var evt = System.Text.Json.JsonSerializer.Deserialize<OrderDetailsFailedEvent>(msg.Payload);
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
                            // do not mark as processed, will retry later
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
