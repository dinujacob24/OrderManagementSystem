namespace Shared.Messages.Commands;

public class CancelOrderCommand
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}
