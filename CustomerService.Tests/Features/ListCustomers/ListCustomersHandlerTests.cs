using CustomerService.Features.ListCustomers;
using CustomerService.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomerService.Tests.Features.ListCustomers;

public class ListCustomersHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoCustomers()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new ListCustomersHandler(db, TestMapperFactory.Create(), NullLogger<ListCustomersHandler>.Instance);

        var result = await sut.Handle(new ListCustomersQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsAllCustomers()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.AddRange(
            new CustomerBuilder().WithId("CUST-001").WithCreatedAt(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Build(),
            new CustomerBuilder().WithId("CUST-002").WithCreatedAt(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)).Build(),
            new CustomerBuilder().WithId("CUST-003").WithCreatedAt(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)).Build());
        await db.SaveChangesAsync();
        var sut = new ListCustomersHandler(db, TestMapperFactory.Create(), NullLogger<ListCustomersHandler>.Instance);

        var result = await sut.Handle(new ListCustomersQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_OrdersByCreatedAtDescending()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.AddRange(
            new CustomerBuilder().WithId("CUST-001").WithCreatedAt(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Build(),
            new CustomerBuilder().WithId("CUST-003").WithCreatedAt(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)).Build(),
            new CustomerBuilder().WithId("CUST-002").WithCreatedAt(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)).Build());
        await db.SaveChangesAsync();
        var sut = new ListCustomersHandler(db, TestMapperFactory.Create(), NullLogger<ListCustomersHandler>.Instance);

        var result = await sut.Handle(new ListCustomersQuery(), CancellationToken.None);

        result.Select(c => c.CustomerId).Should().Equal("CUST-003", "CUST-002", "CUST-001");
    }

    [Fact]
    public async Task Handle_IncludesInactiveCustomers()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.AddRange(
            new CustomerBuilder().WithId("CUST-001").Active().Build(),
            new CustomerBuilder().WithId("CUST-002").Inactive().Build(),
            new CustomerBuilder().WithId("CUST-003").Suspended().Build());
        await db.SaveChangesAsync();
        var sut = new ListCustomersHandler(db, TestMapperFactory.Create(), NullLogger<ListCustomersHandler>.Instance);

        var result = await sut.Handle(new ListCustomersQuery(), CancellationToken.None);

        result.Select(c => c.Status).Should().Contain(new[] { "Active", "Inactive", "Suspended" });
    }

    [Fact]
    public async Task Handle_MapsExpectedFields()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder()
            .WithId("CUST-001")
            .WithFirstName("Alice")
            .WithLastName("Smith")
            .WithEmail("alice@example.com")
            .WithPhone("555")
            .Build());
        await db.SaveChangesAsync();
        var sut = new ListCustomersHandler(db, TestMapperFactory.Create(), NullLogger<ListCustomersHandler>.Instance);

        var result = await sut.Handle(new ListCustomersQuery(), CancellationToken.None);

        var dto = result.Single();
        dto.CustomerId.Should().Be("CUST-001");
        dto.FirstName.Should().Be("Alice");
        dto.LastName.Should().Be("Smith");
        dto.Email.Should().Be("alice@example.com");
        dto.Phone.Should().Be("555");
        dto.Status.Should().Be("Active");
    }
}
