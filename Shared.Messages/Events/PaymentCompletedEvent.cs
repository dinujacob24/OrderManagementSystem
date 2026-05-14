namespace Shared.Messages.Events
{
    public record PaymentCompletedEvent
    {
        public Guid SagaId { get; init; }
        public int OrderId { get; init; }
        public decimal Amount { get; init; }
        public string? TransactionId { get; init; }
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
