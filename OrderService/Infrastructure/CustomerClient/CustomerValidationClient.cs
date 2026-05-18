using System.Net;

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

        public CustomerValidationClient(HttpClient http, ILogger<CustomerValidationClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<CustomerValidationResult> ValidateAsync(string customerId, CancellationToken ct)
        {
            var response = await _http.GetAsync($"/api/customers/{customerId}", ct);

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
