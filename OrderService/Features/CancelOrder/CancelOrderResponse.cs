namespace OrderService.Features.CancelOrder;

public class CancelOrderResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int OrderId { get; set; }
}
