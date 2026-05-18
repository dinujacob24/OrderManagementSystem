namespace Shared.Messages.Events;

public class OrderCancelledEvent
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; }
    public Guid CorrelationId { get; set; }
}
