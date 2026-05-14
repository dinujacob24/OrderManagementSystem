using MassTransit;
using OrderService.Messages.Events;
using OrderService.Saga;

namespace OrderService.Consumers
{
    public class OrderDetailsCompletedConsumer : IConsumer<Shared.Messages.Events.OrderDetailsCompletedEvent>
    {
        private readonly OrderSagaOrchestrator _sagaOrchestrator;
        private readonly ILogger<OrderDetailsCompletedConsumer> _logger;

        public OrderDetailsCompletedConsumer(
            OrderSagaOrchestrator sagaOrchestrator,
            ILogger<OrderDetailsCompletedConsumer> logger)
        {
            _sagaOrchestrator = sagaOrchestrator;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Shared.Messages.Events.OrderDetailsCompletedEvent> context)
        {
            _logger.LogInformation("Received OrderDetailsCompletedEvent for Saga {SagaId}, Success: {Success}", 
                context.Message.SagaId, context.Message.Success);

            await _sagaOrchestrator.HandleOrderDetailsCompleted(context.Message);
        }
    }
}
