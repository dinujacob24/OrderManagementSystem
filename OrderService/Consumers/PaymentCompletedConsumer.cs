using MassTransit;
using OrderService.Saga;

namespace OrderService.Consumers
{
    public class PaymentCompletedConsumer : IConsumer<Shared.Messages.Events.PaymentCompletedEvent>
    {
        private readonly OrderSagaOrchestrator _sagaOrchestrator;
        private readonly ILogger<PaymentCompletedConsumer> _logger;

        public PaymentCompletedConsumer(
            OrderSagaOrchestrator sagaOrchestrator,
            ILogger<PaymentCompletedConsumer> logger)
        {
            _sagaOrchestrator = sagaOrchestrator;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Shared.Messages.Events.PaymentCompletedEvent> context)
        {
            _logger.LogInformation("Received PaymentCompletedEvent for Saga {SagaId}, Success: {Success}", 
                context.Message.SagaId, context.Message.Success);

            await _sagaOrchestrator.HandlePaymentCompleted(context.Message);
        }
    }
}
