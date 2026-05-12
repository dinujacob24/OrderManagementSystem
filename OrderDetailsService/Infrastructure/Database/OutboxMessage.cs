using System.ComponentModel.DataAnnotations;

namespace OrderDetailsService.Infrastructure.Database
{
    public class OutboxMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [MaxLength(200)]
        public string MessageType { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty; // JSON payload

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool Processed { get; set; } = false;

        public DateTime? ProcessedAt { get; set; }
    }
}
