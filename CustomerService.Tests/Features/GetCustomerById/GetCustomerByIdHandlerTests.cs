using CustomerService.Common.Exceptions;
using CustomerService.Features.GetCustomerById;
using CustomerService.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomerService.Tests.Features.GetCustomerById;

public class GetCustomerByIdHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCustomer_WhenFound()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder()
            .WithId("CUST-001")
            .WithFirstName("Alice")
            .WithLastName("Smith")
            .WithEmail("alice@example.com")
            .WithPhone("555")
            .WithAddressLine1("1 Main St")
            .WithCity("Townsville")
            .WithCountry("US")
            .Build());
        await db.SaveChangesAsync();
        var sut = new GetCustomerByIdHandler(db, TestMapperFactory.Create(), NullLogger<GetCustomerByIdHandler>.Instance);

        var result = await sut.Handle(new GetCustomerByIdQuery("CUST-001"), CancellationToken.None);

        result.CustomerId.Should().Be("CUST-001");
        result.FirstName.Should().Be("Alice");
        result.LastName.Should().Be("Smith");
        result.Email.Should().Be("alice@example.com");
        result.Phone.Should().Be("555");
        result.AddressLine1.Should().Be("1 Main St");
        result.City.Should().Be("Townsville");
        result.Country.Should().Be("US");
        result.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_ReturnsInactiveCustomer_Too()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Inactive().Build());
        await db.SaveChangesAsync();
        var sut = new GetCustomerByIdHandler(db, TestMapperFactory.Create(), NullLogger<GetCustomerByIdHandler>.Instance);

        var result = await sut.Handle(new GetCustomerByIdQuery("CUST-001"), CancellationToken.None);

        result.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissing()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new GetCustomerByIdHandler(db, TestMapperFactory.Create(), NullLogger<GetCustomerByIdHandler>.Instance);

        var act = () => sut.Handle(new GetCustomerByIdQuery("CUST-MISSING"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CUST-MISSING*");
    }
}
