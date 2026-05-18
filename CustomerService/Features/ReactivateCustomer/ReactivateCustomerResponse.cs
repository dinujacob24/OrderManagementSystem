namespace CustomerService.Features.ReactivateCustomer;

public record ReactivateCustomerResponse(string CustomerId, string Status, DateTime? UpdatedAt);
