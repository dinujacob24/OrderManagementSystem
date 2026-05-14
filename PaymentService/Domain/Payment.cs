using System.ComponentModel.DataAnnotations;

namespace PaymentService.Domain
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }
        public Guid SagaId { get; set; }
        public int OrderId { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
        public string? PaymentMethod { get; set; }
        public string? TransactionId { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
