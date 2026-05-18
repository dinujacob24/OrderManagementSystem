namespace Shared.Messages
{
    /// <summary>
    /// Constants for message types used in the outbox pattern.
    /// Ensures consistency across all services.
    /// </summary>
    public static class MessageTypes
    {
        // Events
        public const string OrderCreatedEvent = nameof(OrderCreatedEvent);
        public const string OrderDetailsCompletedEvent = nameof(OrderDetailsCompletedEvent);
        public const string OrderDetailsFailedEvent = nameof(OrderDetailsFailedEvent);
        public const string PaymentCompletedEvent = nameof(PaymentCompletedEvent);
        public const string PaymentFailedEvent = nameof(PaymentFailedEvent);
        public const string NotificationCompletedEvent = nameof(NotificationCompletedEvent);
        public const string OrderCompletedEvent = nameof(OrderCompletedEvent);
        public const string OrderCancelledEvent = nameof(OrderCancelledEvent);

        // Commands
        public const string ProcessPaymentCommand = nameof(ProcessPaymentCommand);
        public const string SendNotificationCommand = nameof(SendNotificationCommand);
    }
}
