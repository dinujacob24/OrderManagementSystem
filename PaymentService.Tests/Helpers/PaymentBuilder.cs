using PaymentService.Domain;

namespace PaymentService.Tests.Helpers;

internal sealed class PaymentBuilder
{
    private int _paymentId;
    private Guid _sagaId = Guid.NewGuid();
    private int _orderId = 1;
    private string _customerId = "cust-1";
    private decimal _amount = 100m;
    private string _status = "Completed";
    private string? _paymentMethod = "SimulatedPayment";
    private string? _transactionId = "TXN-ABCDEF12";
    private string? _errorMessage = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _processedAt = DateTime.UtcNow;

    public PaymentBuilder WithPaymentId(int id) { _paymentId = id; return this; }
    public PaymentBuilder WithOrderId(int orderId) { _orderId = orderId; return this; }
    public PaymentBuilder WithSagaId(Guid sagaId) { _sagaId = sagaId; return this; }
    public PaymentBuilder WithCustomerId(string customerId) { _customerId = customerId; return this; }
    public PaymentBuilder WithAmount(decimal amount) { _amount = amount; return this; }
    public PaymentBuilder WithStatus(string status) { _status = status; return this; }
    public PaymentBuilder WithCreatedAt(DateTime createdAt) { _createdAt = createdAt; return this; }

    public Payment Build() => new()
    {
        PaymentId = _paymentId,
        SagaId = _sagaId,
        OrderId = _orderId,
        CustomerId = _customerId,
        Amount = _amount,
        Status = _status,
        PaymentMethod = _paymentMethod,
        TransactionId = _transactionId,
        ErrorMessage = _errorMessage,
        CreatedAt = _createdAt,
        ProcessedAt = _processedAt
    };
}
