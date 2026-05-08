namespace OrderService.DTOs
{
    public record CreateOrderRequest
    {
        public string CustomerId { get; init; } = string.Empty;
        public List<OrderItemDto> Items { get; init; } = new();
    }

    public record OrderItemDto
    {
        public string ProductId { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
    }
}
