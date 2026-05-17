namespace CustomerService.Features.UpdateCustomer;

public record UpdateCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? Country);
