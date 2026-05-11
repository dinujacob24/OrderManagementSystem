namespace OrderDetailsService.Messages.Events
{
    public record OrderDetailsFailedEvent
    {
        public Guid SagaId { get; init; }
        public int OrderId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; }
    }
}
