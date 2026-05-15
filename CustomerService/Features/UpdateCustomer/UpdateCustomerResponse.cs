namespace CustomerService.Features.UpdateCustomer;

public record UpdateCustomerResponse(
    string CustomerId,
    string FirstName,
    string LastName,
    string Email,
    DateTime? UpdatedAt);
