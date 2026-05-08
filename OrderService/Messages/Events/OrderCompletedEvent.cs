namespace OrderService.Messages.Events
{
    public record OrderCompletedEvent
    {
        public Guid SagaId { get; init; }
        public int OrderId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
        public decimal TotalAmount { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
