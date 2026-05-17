using CustomerService.Common.Enums;
using CustomerService.Common.Exceptions;
using CustomerService.Features.CreateCustomer;
using CustomerService.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Tests.Features.CreateCustomer;

public class CreateCustomerHandlerTests
{
    private static CreateCustomerCommand Cmd(
        string id = "CUST-001",
        string firstName = "Alice",
        string lastName = "Smith",
        string email = "alice@example.com",
        string? phone = null,
        string? addressLine1 = null,
        string? city = null,
        string? country = null) =>
        new(new CreateCustomerRequest(id, firstName, lastName, email, phone, addressLine1, city, country));

    [Fact]
    public async Task Handle_PersistsCustomer_AndReturnsResponse()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new CreateCustomerHandler(db, TestMapperFactory.Create());

        var response = await sut.Handle(Cmd(), CancellationToken.None);

        response.CustomerId.Should().Be("CUST-001");
        response.Email.Should().Be("alice@example.com");
        var persisted = await db.Customers.SingleAsync();
        persisted.FirstName.Should().Be("Alice");
        persisted.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task Handle_MapsEveryRequestField()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new CreateCustomerHandler(db, TestMapperFactory.Create());

        await sut.Handle(
            Cmd(phone: "555-1234", addressLine1: "1 Main St", city: "Townsville", country: "US"),
            CancellationToken.None);

        var persisted = await db.Customers.SingleAsync();
        persisted.Phone.Should().Be("555-1234");
        persisted.AddressLine1.Should().Be("1 Main St");
        persisted.City.Should().Be("Townsville");
        persisted.Country.Should().Be("US");
    }

    [Fact]
    public async Task Handle_ForcesStatusActive_RegardlessOfMapping()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new CreateCustomerHandler(db, TestMapperFactory.Create());

        await sut.Handle(Cmd(), CancellationToken.None);

        var persisted = await db.Customers.SingleAsync();
        persisted.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact]
    public async Task Handle_SetsCreatedAt_ToRecentUtcNow()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new CreateCustomerHandler(db, TestMapperFactory.Create());
        var before = DateTime.UtcNow;

        await sut.Handle(Cmd(), CancellationToken.None);

        var after = DateTime.UtcNow;
        var persisted = await db.Customers.SingleAsync();
        persisted.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        persisted.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Handle_LeavesUpdatedAtNull()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new CreateCustomerHandler(db, TestMapperFactory.Create());

        await sut.Handle(Cmd(), CancellationToken.None);

        var persisted = await db.Customers.SingleAsync();
        persisted.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ThrowsConflict_WhenCustomerIdAlreadyExists()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Build());
        await db.SaveChangesAsync();
        var sut = new CreateCustomerHandler(db, TestMapperFactory.Create());

        var act = () => sut.Handle(Cmd("CUST-001"), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*CUST-001*");
    }

    [Fact]
    public async Task Handle_DoesNotPersistAnything_OnConflict()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").WithEmail("first@example.com").Build());
        await db.SaveChangesAsync();
        var sut = new CreateCustomerHandler(db, TestMapperFactory.Create());

        try { await sut.Handle(Cmd("CUST-001", email: "second@example.com"), CancellationToken.None); }
        catch (ConflictException) { }

        var rows = await db.Customers.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Email.Should().Be("first@example.com");
    }
}
