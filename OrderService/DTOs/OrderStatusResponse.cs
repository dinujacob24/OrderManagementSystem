namespace OrderService.DTOs
{
    public record OrderStatusResponse
    {
        public int OrderId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
        public decimal TotalAmount { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime OrderDate { get; init; }
        public SagaStatusDto? SagaStatus { get; init; }
    }

    public record SagaStatusDto
    {
        public Guid SagaId { get; init; }
        public string CurrentStep { get; init; } = string.Empty;
        public bool IsOrderDetailsCompleted { get; init; }
        public bool IsPaymentCompleted { get; init; }
        public bool IsNotificationCompleted { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
