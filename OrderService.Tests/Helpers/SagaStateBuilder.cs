using OrderService.Domain;

namespace OrderService.Tests.Helpers;

internal class SagaStateBuilder
{
    private Guid _sagaId = Guid.NewGuid();
    private int _orderId;
    private string _customerId = "CUST-001";
    private string _currentStep = "OrderCreated";
    private string _status = OrderStatus.Pending;
    private DateTime _startedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private bool _isOrderDetailsCompleted;
    private bool _isPaymentCompleted;
    private bool _isNotificationCompleted;
    private string? _errorMessage;

    public SagaStateBuilder WithSagaId(Guid v) { _sagaId = v; return this; }
    public SagaStateBuilder WithOrderId(int v) { _orderId = v; return this; }
    public SagaStateBuilder WithCustomerId(string v) { _customerId = v; return this; }
    public SagaStateBuilder WithCurrentStep(string v) { _currentStep = v; return this; }
    public SagaStateBuilder WithStatus(string v) { _status = v; return this; }
    public SagaStateBuilder WithOrderDetailsCompleted(bool v = true) { _isOrderDetailsCompleted = v; return this; }
    public SagaStateBuilder WithPaymentCompleted(bool v = true) { _isPaymentCompleted = v; return this; }
    public SagaStateBuilder WithNotificationCompleted(bool v = true) { _isNotificationCompleted = v; return this; }
    public SagaStateBuilder WithErrorMessage(string? v) { _errorMessage = v; return this; }

    public SagaState Build() => new()
    {
        SagaId = _sagaId,
        OrderId = _orderId,
        CustomerId = _customerId,
        CurrentStep = _currentStep,
        Status = _status,
        StartedAt = _startedAt,
        IsOrderDetailsCompleted = _isOrderDetailsCompleted,
        IsPaymentCompleted = _isPaymentCompleted,
        IsNotificationCompleted = _isNotificationCompleted,
        ErrorMessage = _errorMessage
    };
}
