namespace CustomerService.Features.GetCustomerById;

public record GetCustomerByIdResponse(
    string CustomerId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? Country,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
