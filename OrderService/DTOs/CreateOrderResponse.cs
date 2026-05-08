namespace OrderService.DTOs
{
    public record CreateOrderResponse
    {
        public int OrderId { get; init; }
        public Guid SagaId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
        public decimal TotalAmount { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime OrderDate { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}
