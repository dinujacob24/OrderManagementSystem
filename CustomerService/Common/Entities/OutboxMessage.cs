using System.ComponentModel.DataAnnotations;

namespace CustomerService.Common.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string MessageType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool Processed { get; set; } = false;

    public DateTime? ProcessedAt { get; set; }

    public int Attempts { get; set; } = 0;

    public string? LastError { get; set; }

    public Guid? LockToken { get; set; }

    public DateTime? LockExpiresAt { get; set; }
}
