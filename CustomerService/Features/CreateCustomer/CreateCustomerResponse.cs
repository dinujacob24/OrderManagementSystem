namespace CustomerService.Features.CreateCustomer;

public record CreateCustomerResponse(string CustomerId, string Email, DateTime CreatedAt);
