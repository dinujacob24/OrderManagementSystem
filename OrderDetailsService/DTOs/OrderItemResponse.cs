namespace OrderDetailsService.DTOs
{
    public record OrderItemResponse
    {
        public int OrderItemId { get; init; }
        public int OrderId { get; init; }
        public string ProductId { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal TotalPrice { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
    }
}
