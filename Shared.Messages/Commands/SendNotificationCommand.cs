namespace Shared.Messages.Commands
{
    public record SendNotificationCommand
    {
        public Guid SagaId { get; init; }
        public int OrderId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string NotificationType { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
    }
}
