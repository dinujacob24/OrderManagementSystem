using OrderDetailsService.Domain;

namespace OrderDetailsService.Tests.Helpers;

internal class OrderItemBuilder
{
    private int _orderId = 1;
    private string _productId = "PROD-001";
    private string _productName = "Sample Product";
    private int _quantity = 1;
    private decimal _unitPrice = 10m;
    private decimal? _totalPrice;
    private string _status = OrderItemStatus.Added;
    private DateTime _createdAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime? _updatedAt;

    public OrderItemBuilder WithOrderId(int v) { _orderId = v; return this; }
    public OrderItemBuilder WithProductId(string v) { _productId = v; return this; }
    public OrderItemBuilder WithProductName(string v) { _productName = v; return this; }
    public OrderItemBuilder WithQuantity(int v) { _quantity = v; return this; }
    public OrderItemBuilder WithUnitPrice(decimal v) { _unitPrice = v; return this; }
    public OrderItemBuilder WithTotalPrice(decimal v) { _totalPrice = v; return this; }
    public OrderItemBuilder WithStatus(string v) { _status = v; return this; }
    public OrderItemBuilder WithCreatedAt(DateTime v) { _createdAt = v; return this; }
    public OrderItemBuilder WithUpdatedAt(DateTime? v) { _updatedAt = v; return this; }

    public OrderItem Build() => new()
    {
        OrderId = _orderId,
        ProductId = _productId,
        ProductName = _productName,
        Quantity = _quantity,
        UnitPrice = _unitPrice,
        TotalPrice = _totalPrice ?? _quantity * _unitPrice,
        Status = _status,
        CreatedAt = _createdAt,
        UpdatedAt = _updatedAt
    };
}
