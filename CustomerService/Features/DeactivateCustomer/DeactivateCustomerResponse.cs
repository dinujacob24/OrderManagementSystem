namespace CustomerService.Features.DeactivateCustomer;

public record DeactivateCustomerResponse(string CustomerId, string Status, DateTime? UpdatedAt);
