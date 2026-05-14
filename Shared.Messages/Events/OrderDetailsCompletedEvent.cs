namespace Shared.Messages.Events
{
    public record OrderDetailsCompletedEvent
    {
        public Guid SagaId { get; init; }
        public int OrderId { get; init; }
        public List<OrderItemDto> Items { get; init; } = new();
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime Timestamp { get; init; }
    }

    public record OrderItemDto
    {
        public int OrderItemId { get; init; }
        public string ProductId { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal TotalPrice { get; init; }
    }
}
