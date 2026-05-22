using OrderService.Domain;

namespace OrderService.Tests.Helpers;

internal class OrderBuilder
{
    private string _customerId = "CUST-001";
    private DateTime _orderDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private decimal _totalAmount = 100m;
    private string _status = OrderStatus.Pending;
    private DateTime _createdAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime? _updatedAt;

    public OrderBuilder WithCustomerId(string v) { _customerId = v; return this; }
    public OrderBuilder WithOrderDate(DateTime v) { _orderDate = v; return this; }
    public OrderBuilder WithTotalAmount(decimal v) { _totalAmount = v; return this; }
    public OrderBuilder WithStatus(string v) { _status = v; return this; }
    public OrderBuilder WithCreatedAt(DateTime v) { _createdAt = v; return this; }
    public OrderBuilder WithUpdatedAt(DateTime? v) { _updatedAt = v; return this; }
    public OrderBuilder Pending() { _status = OrderStatus.Pending; return this; }
    public OrderBuilder Completed() { _status = OrderStatus.Completed; return this; }
    public OrderBuilder Cancelled() { _status = OrderStatus.Cancelled; return this; }

    public Order Build() => new()
    {
        CustomerId = _customerId,
        OrderDate = _orderDate,
        TotalAmount = _totalAmount,
        Status = _status,
        CreatedAt = _createdAt,
        UpdatedAt = _updatedAt
    };
}
