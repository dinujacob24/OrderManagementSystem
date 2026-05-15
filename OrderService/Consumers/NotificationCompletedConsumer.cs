using MassTransit;
//using OrderService.Messages.Events;
using OrderService.Saga;
using NotificationCompletedEvent = Shared.Messages.Events.NotificationCompletedEvent;

namespace OrderService.Consumers
{
    public class NotificationCompletedConsumer : IConsumer<NotificationCompletedEvent>
    {
        private readonly OrderSagaOrchestrator _sagaOrchestrator;
        private readonly ILogger<NotificationCompletedConsumer> _logger;

        public NotificationCompletedConsumer(
            OrderSagaOrchestrator sagaOrchestrator,
            ILogger<NotificationCompletedConsumer> logger)
        {
            _sagaOrchestrator = sagaOrchestrator;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<NotificationCompletedEvent> context)
        {
            _logger.LogInformation("Received NotificationCompletedEvent for Saga {SagaId}, Success: {Success}", 
                context.Message.SagaId, context.Message.Success);

            await _sagaOrchestrator.HandleNotificationCompleted(context.Message);
        }
    }
}
