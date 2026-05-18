namespace Shared.Messages.Commands;

public class ProcessRefundCommand
{
    public int OrderId { get; set; }
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public Guid CorrelationId { get; set; }
}
