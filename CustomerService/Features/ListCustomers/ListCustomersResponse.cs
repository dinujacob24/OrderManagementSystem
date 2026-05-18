namespace CustomerService.Features.ListCustomers;

public record ListCustomersResponse(
    string CustomerId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Status,
    DateTime CreatedAt);
