namespace Shared.Messages.Events
{
    public record OrderCreatedEvent
    {
        public Guid SagaId { get; init; }
        public int OrderId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
        public List<OrderItemEventDto> Items { get; init; } = new();
        public decimal TotalAmount { get; init; }
        public DateTime Timestamp { get; init; }
    }

    public record OrderItemEventDto
    {
        public string ProductId { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
    }
}
