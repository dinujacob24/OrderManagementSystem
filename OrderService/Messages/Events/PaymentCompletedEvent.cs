namespace OrderService.Messages.Events
{
    public record PaymentCompletedEvent
    {
        public Guid SagaId { get; init; }
        public int OrderId { get; init; }
        public string TransactionId { get; init; } = string.Empty;
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
