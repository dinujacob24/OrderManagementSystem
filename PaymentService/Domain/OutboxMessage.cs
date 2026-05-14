using System.ComponentModel.DataAnnotations;

namespace PaymentService.Domain
{
    public class OutboxMessage
    {
        [Key]
        public Guid Id { get; set; }
        public string MessageType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public bool Processed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int Attempts { get; set; }
        public string? LastError { get; set; }
        public Guid? LockToken { get; set; }
        public DateTime? LockExpiresAt { get; set; }
    }
}
