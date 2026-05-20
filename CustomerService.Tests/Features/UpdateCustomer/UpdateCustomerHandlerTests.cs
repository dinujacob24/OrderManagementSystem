using CustomerService.Common.Exceptions;
using CustomerService.Features.UpdateCustomer;
using CustomerService.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomerService.Tests.Features.UpdateCustomer;

public class UpdateCustomerHandlerTests
{
    private static UpdateCustomerCommand Cmd(
        string id = "CUST-001",
        string firstName = "Updated",
        string lastName = "User",
        string email = "updated@example.com",
        string? phone = "999",
        string? addressLine1 = "Updated Address",
        string? city = "NewCity",
        string? country = "CA") =>
        new(id, new UpdateCustomerRequest(firstName, lastName, email, phone, addressLine1, city, country));

    [Fact]
    public async Task Handle_UpdatesAllFields()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Build());
        await db.SaveChangesAsync();
        var sut = new UpdateCustomerHandler(db, TestMapperFactory.Create(), NullLogger<UpdateCustomerHandler>.Instance);

        await sut.Handle(Cmd(), CancellationToken.None);

        var persisted = await db.Customers.SingleAsync();
        persisted.FirstName.Should().Be("Updated");
        persisted.LastName.Should().Be("User");
        persisted.Email.Should().Be("updated@example.com");
        persisted.Phone.Should().Be("999");
        persisted.AddressLine1.Should().Be("Updated Address");
        persisted.City.Should().Be("NewCity");
        persisted.Country.Should().Be("CA");
    }

    [Fact]
    public async Task Handle_SetsUpdatedAt_ToRecentUtcNow()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Build());
        await db.SaveChangesAsync();
        var sut = new UpdateCustomerHandler(db, TestMapperFactory.Create(), NullLogger<UpdateCustomerHandler>.Instance);
        var before = DateTime.UtcNow;

        await sut.Handle(Cmd(), CancellationToken.None);

        var after = DateTime.UtcNow;
        var persisted = await db.Customers.SingleAsync();
        persisted.UpdatedAt.Should().NotBeNull();
        persisted.UpdatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_ReturnsMappedResponse()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Build());
        await db.SaveChangesAsync();
        var sut = new UpdateCustomerHandler(db, TestMapperFactory.Create(), NullLogger<UpdateCustomerHandler>.Instance);

        var response = await sut.Handle(Cmd(), CancellationToken.None);

        response.CustomerId.Should().Be("CUST-001");
        response.FirstName.Should().Be("Updated");
        response.LastName.Should().Be("User");
        response.Email.Should().Be("updated@example.com");
        response.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_OverwritesOptionalFields_WithNulls()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder()
            .WithId("CUST-001")
            .WithPhone("555")
            .WithAddressLine1("Old Address")
            .WithCity("OldCity")
            .WithCountry("US")
            .Build());
        await db.SaveChangesAsync();
        var sut = new UpdateCustomerHandler(db, TestMapperFactory.Create(), NullLogger<UpdateCustomerHandler>.Instance);

        await sut.Handle(Cmd(phone: null, addressLine1: null, city: null, country: null), CancellationToken.None);

        var persisted = await db.Customers.SingleAsync();
        persisted.Phone.Should().BeNull();
        persisted.AddressLine1.Should().BeNull();
        persisted.City.Should().BeNull();
        persisted.Country.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PreservesStatus_AndCreatedAt()
    {
        await using var db = TestDbContextFactory.Create();
        var originalCreated = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Customers.Add(new CustomerBuilder()
            .WithId("CUST-001")
            .Inactive()
            .WithCreatedAt(originalCreated)
            .Build());
        await db.SaveChangesAsync();
        var sut = new UpdateCustomerHandler(db, TestMapperFactory.Create(), NullLogger<UpdateCustomerHandler>.Instance);

        await sut.Handle(Cmd(), CancellationToken.None);

        var persisted = await db.Customers.SingleAsync();
        persisted.Status.Should().Be("Inactive");
        persisted.CreatedAt.Should().Be(originalCreated);
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissing()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new UpdateCustomerHandler(db, TestMapperFactory.Create(), NullLogger<UpdateCustomerHandler>.Instance);

        var act = () => sut.Handle(Cmd("CUST-MISSING"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CUST-MISSING*");
    }
}
