namespace PaymentService.Domain;

public class Refund
{
    public Guid Id { get; set; }
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty; // Pending, Completed, Failed
    public DateTime ProcessedAt { get; set; }
}
