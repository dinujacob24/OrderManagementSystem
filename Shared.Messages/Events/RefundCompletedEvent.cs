namespace Shared.Messages.Events;

public class RefundCompletedEvent
{
    public int OrderId { get; set; }
    public int PaymentId { get; set; }
    public decimal RefundAmount { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}
