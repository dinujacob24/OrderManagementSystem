using CustomerService.Common.Entities;
using CustomerService.Common.Enums;

namespace CustomerService.Tests.Helpers;

internal class CustomerBuilder
{
    private string _customerId = "CUST-001";
    private string _firstName = "Alice";
    private string _lastName = "Smith";
    private string _email = "alice@example.com";
    private string? _phone;
    private string? _addressLine1;
    private string? _city;
    private string? _country;
    private string _status = CustomerStatus.Active;
    private DateTime _createdAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private DateTime? _updatedAt;

    public CustomerBuilder WithId(string id) { _customerId = id; return this; }
    public CustomerBuilder WithFirstName(string v) { _firstName = v; return this; }
    public CustomerBuilder WithLastName(string v) { _lastName = v; return this; }
    public CustomerBuilder WithEmail(string v) { _email = v; return this; }
    public CustomerBuilder WithPhone(string? v) { _phone = v; return this; }
    public CustomerBuilder WithAddressLine1(string? v) { _addressLine1 = v; return this; }
    public CustomerBuilder WithCity(string? v) { _city = v; return this; }
    public CustomerBuilder WithCountry(string? v) { _country = v; return this; }
    public CustomerBuilder WithStatus(string v) { _status = v; return this; }
    public CustomerBuilder WithCreatedAt(DateTime v) { _createdAt = v; return this; }
    public CustomerBuilder WithUpdatedAt(DateTime? v) { _updatedAt = v; return this; }
    public CustomerBuilder Active() { _status = CustomerStatus.Active; return this; }
    public CustomerBuilder Inactive() { _status = CustomerStatus.Inactive; return this; }
    public CustomerBuilder Suspended() { _status = CustomerStatus.Suspended; return this; }

    public Customer Build() => new()
    {
        CustomerId = _customerId,
        FirstName = _firstName,
        LastName = _lastName,
        Email = _email,
        Phone = _phone,
        AddressLine1 = _addressLine1,
        City = _city,
        Country = _country,
        Status = _status,
        CreatedAt = _createdAt,
        UpdatedAt = _updatedAt
    };
}
