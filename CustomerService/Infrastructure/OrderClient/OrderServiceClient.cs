using System.Net.Http.Headers;

namespace CustomerService.Infrastructure.OrderClient;

public interface IOrderServiceClient
{
    Task<OrderCancellationResult> CancelCustomerOrdersAsync(string customerId, string reason, CancellationToken ct);
}

public record OrderCancellationResult(bool Success, int CancelledOrdersCount, string? ErrorMessage)
{
    public static OrderCancellationResult Ok(int count) => new(true, count, null);
    public static OrderCancellationResult Fail(string error) => new(false, 0, error);
}

public class OrderServiceClient : IOrderServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OrderServiceClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrderServiceClient(
        HttpClient http,
        ILogger<OrderServiceClient> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<OrderCancellationResult> CancelCustomerOrdersAsync(
        string customerId,
        string reason,
        CancellationToken ct)
    {
        try
        {
            // Forward the Authorization header (JWT token) from the incoming request
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                _http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);
            }

            var request = new CancelCustomerOrdersRequest(customerId, reason);
            var response = await _http.PostAsJsonAsync("/api/orders/cancel-customer-orders", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "OrderService returned {StatusCode} when cancelling orders for customer {CustomerId}: {Error}",
                    response.StatusCode, customerId, errorContent);

                return OrderCancellationResult.Fail(
                    $"OrderService returned status {(int)response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<CancelCustomerOrdersResponse>(cancellationToken: ct);

            if (result == null)
            {
                return OrderCancellationResult.Fail("OrderService returned empty response");
            }

            _logger.LogInformation(
                "Successfully cancelled {Count} orders for customer {CustomerId}",
                result.CancelledOrdersCount, customerId);

            return OrderCancellationResult.Ok(result.CancelledOrdersCount);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP error while cancelling orders for customer {CustomerId}",
                customerId);
            return OrderCancellationResult.Fail($"Connection error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while cancelling orders for customer {CustomerId}",
                customerId);
            return OrderCancellationResult.Fail($"Unexpected error: {ex.Message}");
        }
    }

    private record CancelCustomerOrdersRequest(string CustomerId, string Reason);
    private record CancelCustomerOrdersResponse(int CancelledOrdersCount);
}
