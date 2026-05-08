namespace OrderService.Messages.Events
{
    public record OrderDetailsCompletedEvent
    {
        public Guid SagaId { get; init; }
        public int OrderId { get; init; }
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
