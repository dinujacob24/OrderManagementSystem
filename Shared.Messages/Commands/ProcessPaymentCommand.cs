namespace Shared.Messages.Commands
{
    public record ProcessPaymentCommand
    {
        public Guid SagaId { get; init; }
        public int OrderId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
