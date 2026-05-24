using PaymentService.Domain;

namespace PaymentService.Tests.Helpers;

internal sealed class OutboxMessageBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _messageType = "ProcessPaymentCommand";
    private string _payload = "{}";
    private bool _processed;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _processedAt;
    private int _attempts;
    private string? _lastError = null;

    public OutboxMessageBuilder WithMessageType(string type) { _messageType = type; return this; }
    public OutboxMessageBuilder WithPayload(string payload) { _payload = payload; return this; }
    public OutboxMessageBuilder Processed() { _processed = true; _processedAt = DateTime.UtcNow; return this; }
    public OutboxMessageBuilder WithCreatedAt(DateTime ts) { _createdAt = ts; return this; }
    public OutboxMessageBuilder WithAttempts(int n) { _attempts = n; return this; }

    public OutboxMessage Build() => new()
    {
        Id = _id,
        MessageType = _messageType,
        Payload = _payload,
        Processed = _processed,
        CreatedAt = _createdAt,
        ProcessedAt = _processedAt,
        Attempts = _attempts,
        LastError = _lastError
    };
}
