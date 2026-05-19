using System.Net;
using System.Net.Http.Headers;

namespace OrderService.Infrastructure.CustomerClient
{
    public interface ICustomerValidationClient
    {
        Task<CustomerValidationResult> ValidateAsync(string customerId, CancellationToken ct);
    }

    public record CustomerValidationResult(bool IsValid, string? Reason)
    {
        public static CustomerValidationResult Ok() => new(true, null);
        public static CustomerValidationResult Fail(string reason) => new(false, reason);
    }

    public class CustomerValidationClient : ICustomerValidationClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<CustomerValidationClient> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomerValidationClient(
            HttpClient http, 
            ILogger<CustomerValidationClient> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _http = http;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<CustomerValidationResult> ValidateAsync(string customerId, CancellationToken ct)
        {
            // Create a new request message to add headers per-request
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"api/customers/{customerId}");

            // Forward the Authorization header (JWT token) from the incoming request
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                {
                    _logger.LogInformation("========== TOKEN FORWARDING DEBUG ==========");
                    _logger.LogInformation("Original header: {Header}", authHeader.Substring(0, Math.Min(50, authHeader.Length)) + "...");

                    // Parse Bearer token and add to Authorization header properly
                    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        var token = authHeader.Substring("Bearer ".Length).Trim();
                        _logger.LogInformation("Token length: {Length}", token.Length);

                        // Use the proper Authorization property
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                        _logger.LogInformation("Authorization header set using AuthenticationHeaderValue");
                        _logger.LogInformation("Header is present: {Present}", requestMessage.Headers.Authorization != null);
                    }
                    else
                    {
                        _logger.LogWarning("Authorization header doesn't start with 'Bearer '");
                    }

                    _logger.LogInformation("==========================================");
                }
                else
                {
                    _logger.LogWarning("No Authorization header found in the incoming request to forward to CustomerService");
                }
            }
            else
            {
                _logger.LogWarning("HttpContext is null, cannot forward Authorization header");
            }

            _logger.LogInformation("Sending request to: {Url}", $"{_http.BaseAddress}{requestMessage.RequestUri}");
            var response = await _http.SendAsync(requestMessage, ct);
            _logger.LogInformation("CustomerService responded with status: {StatusCode}", response.StatusCode);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return CustomerValidationResult.Fail($"Customer '{customerId}' not found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CustomerService returned {StatusCode} validating customer {CustomerId}",
                    response.StatusCode, customerId);
                return CustomerValidationResult.Fail(
                    $"Customer service unavailable (status {(int)response.StatusCode}).");
            }

            var customer = await response.Content.ReadFromJsonAsync<CustomerDto>(cancellationToken: ct);
            if (customer is null)
            {
                return CustomerValidationResult.Fail("Customer service returned an empty response.");
            }

            if (!string.Equals(customer.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return CustomerValidationResult.Fail(
                    $"Customer '{customerId}' is not active (status: {customer.Status}).");
            }

            return CustomerValidationResult.Ok();
        }

        private record CustomerDto(string CustomerId, string Status);
    }
}
