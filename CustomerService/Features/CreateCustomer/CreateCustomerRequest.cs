namespace CustomerService.Features.CreateCustomer;

public record CreateCustomerRequest(
    string CustomerId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? Country);
