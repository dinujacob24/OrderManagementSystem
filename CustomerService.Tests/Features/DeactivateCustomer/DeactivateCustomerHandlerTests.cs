using CustomerService.Common.Enums;
using CustomerService.Common.Exceptions;
using CustomerService.Features.DeactivateCustomer;
using CustomerService.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CustomerService.Tests.Features.DeactivateCustomer;

public class DeactivateCustomerHandlerTests
{
    [Fact]
    public async Task Handle_SetsStatusInactive_WhenActive()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Active().Build());
        await db.SaveChangesAsync();
        var sut = new DeactivateCustomerHandler(db);

        var response = await sut.Handle(new DeactivateCustomerCommand("CUST-001"), CancellationToken.None);

        response.Status.Should().Be(CustomerStatus.Inactive);
        var persisted = await db.Customers.SingleAsync();
        persisted.Status.Should().Be(CustomerStatus.Inactive);
    }

    [Fact]
    public async Task Handle_SetsUpdatedAt_WhenTransitioning()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Active().WithUpdatedAt(null).Build());
        await db.SaveChangesAsync();
        var sut = new DeactivateCustomerHandler(db);
        var before = DateTime.UtcNow;

        await sut.Handle(new DeactivateCustomerCommand("CUST-001"), CancellationToken.None);

        var after = DateTime.UtcNow;
        var persisted = await db.Customers.SingleAsync();
        persisted.UpdatedAt.Should().NotBeNull();
        persisted.UpdatedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_TransitionsSuspendedToInactive()
    {
        await using var db = TestDbContextFactory.Create();
        db.Customers.Add(new CustomerBuilder().WithId("CUST-001").Suspended().Build());
        await db.SaveChangesAsync();
        var sut = new DeactivateCustomerHandler(db);

        var response = await sut.Handle(new DeactivateCustomerCommand("CUST-001"), CancellationToken.None);

        response.Status.Should().Be(CustomerStatus.Inactive);
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenAlreadyInactive()
    {
        await using var db = TestDbContextFactory.Create();
        var existingUpdatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        db.Customers.Add(new CustomerBuilder()
            .WithId("CUST-001")
            .Inactive()
            .WithUpdatedAt(existingUpdatedAt)
            .Build());
        await db.SaveChangesAsync();
        var sut = new DeactivateCustomerHandler(db);

        var response = await sut.Handle(new DeactivateCustomerCommand("CUST-001"), CancellationToken.None);

        response.Status.Should().Be(CustomerStatus.Inactive);
        var persisted = await db.Customers.SingleAsync();
        persisted.UpdatedAt.Should().Be(existingUpdatedAt);
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenMissing()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new DeactivateCustomerHandler(db);

        var act = () => sut.Handle(new DeactivateCustomerCommand("CUST-MISSING"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*CUST-MISSING*");
    }
}
