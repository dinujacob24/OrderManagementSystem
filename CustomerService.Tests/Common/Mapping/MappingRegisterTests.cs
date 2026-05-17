using CustomerService.Common.Entities;
using CustomerService.Common.Enums;
using CustomerService.Features.CreateCustomer;
using CustomerService.Features.GetCustomerById;
using CustomerService.Features.ListCustomers;
using CustomerService.Features.UpdateCustomer;
using CustomerService.Tests.Helpers;
using FluentAssertions;

namespace CustomerService.Tests.Common.Mapping;

public class MappingRegisterTests
{
    private readonly MapsterMapper.IMapper _mapper = TestMapperFactory.Create();

    [Fact]
    public void CreateCustomerRequest_to_Customer_maps_all_fields_except_ignored()
    {
        var request = new CreateCustomerRequest(
            "CUST-001", "Alice", "Smith", "alice@example.com",
            "555", "1 Main St", "Townsville", "US");

        var customer = _mapper.Map<Customer>(request);

        customer.CustomerId.Should().Be("CUST-001");
        customer.FirstName.Should().Be("Alice");
        customer.LastName.Should().Be("Smith");
        customer.Email.Should().Be("alice@example.com");
        customer.Phone.Should().Be("555");
        customer.AddressLine1.Should().Be("1 Main St");
        customer.City.Should().Be("Townsville");
        customer.Country.Should().Be("US");
    }

    [Fact]
    public void CreateCustomerRequest_to_Customer_leaves_Status_CreatedAt_UpdatedAt_at_entity_defaults()
    {
        var request = new CreateCustomerRequest(
            "CUST-001", "Alice", "Smith", "alice@example.com",
            null, null, null, null);
        var before = DateTime.UtcNow;

        var customer = _mapper.Map<Customer>(request);

        customer.Status.Should().Be(CustomerStatus.Active);
        customer.CreatedAt.Should().BeOnOrAfter(before);
        customer.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Customer_to_CreateCustomerResponse_maps_fields()
    {
        var customer = new CustomerBuilder()
            .WithId("CUST-001")
            .WithEmail("alice@example.com")
            .WithCreatedAt(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .Build();

        var response = _mapper.Map<CreateCustomerResponse>(customer);

        response.CustomerId.Should().Be("CUST-001");
        response.Email.Should().Be("alice@example.com");
        response.CreatedAt.Should().Be(customer.CreatedAt);
    }

    [Fact]
    public void Customer_to_GetCustomerByIdResponse_maps_all_fields()
    {
        var customer = new CustomerBuilder()
            .WithId("CUST-001")
            .WithFirstName("Alice")
            .WithLastName("Smith")
            .WithEmail("alice@example.com")
            .WithPhone("555")
            .WithAddressLine1("Addr")
            .WithCity("City")
            .WithCountry("US")
            .Inactive()
            .WithUpdatedAt(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc))
            .Build();

        var response = _mapper.Map<GetCustomerByIdResponse>(customer);

        response.CustomerId.Should().Be("CUST-001");
        response.FirstName.Should().Be("Alice");
        response.LastName.Should().Be("Smith");
        response.Email.Should().Be("alice@example.com");
        response.Phone.Should().Be("555");
        response.AddressLine1.Should().Be("Addr");
        response.City.Should().Be("City");
        response.Country.Should().Be("US");
        response.Status.Should().Be(CustomerStatus.Inactive);
        response.CreatedAt.Should().Be(customer.CreatedAt);
        response.UpdatedAt.Should().Be(customer.UpdatedAt);
    }

    [Fact]
    public void Customer_to_ListCustomersResponse_maps_summary_fields()
    {
        var customer = new CustomerBuilder()
            .WithId("CUST-001")
            .WithFirstName("Alice")
            .WithLastName("Smith")
            .WithEmail("alice@example.com")
            .WithPhone("555")
            .Build();

        var response = _mapper.Map<ListCustomersResponse>(customer);

        response.CustomerId.Should().Be("CUST-001");
        response.FirstName.Should().Be("Alice");
        response.LastName.Should().Be("Smith");
        response.Email.Should().Be("alice@example.com");
        response.Phone.Should().Be("555");
        response.Status.Should().Be(CustomerStatus.Active);
        response.CreatedAt.Should().Be(customer.CreatedAt);
    }

    [Fact]
    public void Customer_to_UpdateCustomerResponse_maps_fields()
    {
        var customer = new CustomerBuilder()
            .WithId("CUST-001")
            .WithFirstName("Alice")
            .WithLastName("Smith")
            .WithEmail("alice@example.com")
            .WithUpdatedAt(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc))
            .Build();

        var response = _mapper.Map<UpdateCustomerResponse>(customer);

        response.CustomerId.Should().Be("CUST-001");
        response.FirstName.Should().Be("Alice");
        response.LastName.Should().Be("Smith");
        response.Email.Should().Be("alice@example.com");
        response.UpdatedAt.Should().Be(customer.UpdatedAt);
    }
}
